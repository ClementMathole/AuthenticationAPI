import http from 'k6/http';
import crypto from 'k6/crypto';
import { check, fail, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const api = __ENV.BASE_URL || 'http://api:8080';
const mailpit = __ENV.MAILPIT_URL || 'http://mailpit:8025';
const completedFlows = new Counter('completed_flows');

export const options = {
    scenarios: {
        authentication: {
            executor: 'shared-iterations',
            vus: 1,
            iterations: 1,
            maxDuration: '5m',
        },
    },
    thresholds: {
        checks: ['rate==1'],
        completed_flows: ['count==1'],
    },
    systemTags: [
        'status',
        'method',
        'name',
        'check',
        'scenario',
        'expected_response',
    ],
};

http.setResponseCallback(http.expectedStatuses(200, 202, 204, 400, 401, 423));

function assert(value, name) {
    if (!check(value, { [name]: v => Boolean(v) }))
        fail(name);
}

function status(response, expected, name) {
    if (!check(response, { [name]: r => r.status === expected }))
        fail(name + ': expected ' + expected + ', got ' + response.status);
    return response;
}

function params(accessToken, name) {
    const headers = { 'Content-Type': 'application/json' };
    if (accessToken)
        headers.Authorization = 'Bearer ' + accessToken;

    return {
        headers,
        tags: { name },
        timeout: '60s',
        redirects: 0,
    };
}

function post(path, body, accessToken) {
    return http.post(
        api + '/api/authentication/' + path,
        JSON.stringify(body),
        params(accessToken, path)
    );
}

function login(account) {
    return post('login', {
        email: account.email,
        password: account.password,
    });
}

function profile(accessToken) {
    return http.get(
        api + '/api/authentication/me',
        params(accessToken, 'me')
    );
}

function register() {
    const id = crypto.sha256(crypto.randomBytes(16), 'hex').slice(0, 20);
    const account = {
        username: 'k6-' + id,
        email: 'k6-' + id + '@example.test',
        password: 'Test-password-' + id + '!',
    };

    status(post('register', account), 202, 'Registration accepted');
    return account;
}

function confirmation(email, previousToken) {
    for (let attempt = 0; attempt < 20; attempt++) {
        const result = status(
          http.get(
            mailpit + '/api/v1/search?query=' + encodeURIComponent('to:' + email),
            params(null, 'mail-search')
          ),
          200,
          'Inbox search succeeds'
        );

        for (const item of result.json().messages || []) {
            const message = status(
                http.get(
                  mailpit + '/api/v1/message/' + item.ID,
                  params(null, 'mail-message')
                ),
              200,
              'Confirmation email received').json();

            const match = /userId=([a-f0-9-]{36})&token=([a-f0-9]{64})/i.exec(message.Text || '');
            if (match && match[2] !== previousToken)
                return {userId: match[1], token: match[2]};
        }
        sleep(0.25);
    }
    assert(false, 'Confirmation email arrived before timeout');
}

function confirm(link) {
    return http.get(
        api + '/api/authentication/confirm?userId=' + link.userId + '&token=' + link.token,
        params(null, 'confirm')
    );
}

export default function () {
    completedFlows.add(0);

    status(
      http.get(api + '/health/ready', params(null, 'ready')),
      200,
      'Database ready'
    );

    const alice = register();
    const oldLink = confirmation(alice.email);

    status(login(alice), 401, 'Unconfirmed account cannot log in');
    status(
        post('refresh', { refreshToken: oldLink.token }),
        401,
        'Confirmation token cannot refresh a session'
    );

    status(
        post('resend-confirmation', { email: alice.email }),
        202,
        'Resend accepted'
    );

    const newLink = confirmation(alice.email, oldLink.token);
    status(
        confirm(oldLink), 400,
        'Resend invalidates previous confirmation link'
    );

    status(confirm(newLink), 200, 'New confirmation succeeds');

    status(
        confirm(newLink), 400,
        'Confirmation token cannot be reused');

    const a0 = status(
        login(alice), 200, 'Confirmed account logs in').json();

    const me = status(
        profile(a0.accessToken), 200,
        'Valid session accesses profile').json();

    assert(me.id === newLink.userId, 'JWT subject matches account');

    const bob = register();
    status(
        confirm(confirmation(bob.email)),
        200,
        'Second account confirmed');

    const b0 = status(
        login(bob), 200, 'Second account logs in').json();

    status(
        post('revoke', { revokeToken: b0.refreshToken }, a0.accessToken),
        204,
        'Foreign-token revocation returns the same response');

    status(
        profile(b0.accessToken),
        200,
        'Another user cannot revoke this session');

    status(
        post('revoke', { revokeToken: b0.refreshToken }, b0.accessToken),
        204,
        'Owner revokes session');

    status(
        profile(b0.accessToken),
        401,
        'Revoked access token is rejected');

    status(
        post('refresh', { refreshToken: b0.refreshToken }),
        401,
        'Revoked session cannot refresh');

    for (let i = 1; i <= 5; i++) {
        status(
            post('login', {
                email: bob.email,
                password: 'Incorrect-password-value!',
            }),
            i < 5 ? 401 : 423,
            'Failed login ' + i);
    }

    status(
        login(bob),
        423,
        'Locked account rejects correct password');

    const a1 = status(
        post('refresh', { refreshToken: a0.refreshToken }),
        200,
        'Refresh rotates tokens').json();

    assert(
        a1.refreshToken !== a0.refreshToken,
        'Refresh secret changes');

    assert(
        a1.accessToken !== a0.accessToken,
        'Access JWT changes');

    status(
        post('refresh', { refreshToken: a0.refreshToken }),
        401,
        'Rotated token reuse is rejected');

    status(
        post('refresh', { refreshToken: a1.refreshToken }),
        401,
        'Replay revokes the replacement token');

    status(
        profile(a1.accessToken),
        401,
        'Replay also invalidates session access');

    const fresh = status(
        login(alice),
        200,
        'New login creates an independent session').json();

    const request = () => ({
        method: 'POST',
        url: api + '/api/authentication/refresh',
        body: JSON.stringify({
            refreshToken: fresh.refreshToken,
        }),
        params: params(null, 'concurrent-refresh'),
    });

    const results = http.batch([request(), request()]);
    assert(
      results.map(r => r.status).sort().join(',') === '200,401',
      'Competing refresh requests have exactly one winner');

    const winner = results.find(r => r.status === 200).json();
    status(
      post(
        'refresh',
        { refreshToken: winner.refreshToken }
      ),
      401,
      'Concurrent replay revokes the winning replacement'
    );
  status(
    profile(winner.accessToken), 401, 'Concurrent replay invalidates session access');

    completedFlows.add(1);
}
