import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5214';

export const options = {
  vus: 1,
  iterations: 5,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<2000']
  }
};

export default function () {
  const createPayload = JSON.stringify({
    amount: 99.9,
    customerEmail: `smoke-${__VU}-${__ITER}-${Date.now()}@example.com`
  });

  const createResponse = http.post(`${baseUrl}/orders`, createPayload, {
    headers: {
      'Content-Type': 'application/json',
      'x-correlation-id': `k6-smoke-${__VU}-${__ITER}`
    }
  });

  const created = check(createResponse, {
    'POST /orders status is 201': (r) => r.status === 201,
    'POST /orders has orderId': (r) => {
      if (!r.body) return false;
      const parsed = JSON.parse(r.body);
      return typeof parsed.orderId === 'string' && parsed.orderId.length > 0;
    }
  });

  if (!created) {
    sleep(1);
    return;
  }

  const orderId = JSON.parse(createResponse.body).orderId;
  let getByIdResponse = null;
  for (let attempt = 0; attempt < 10; attempt += 1) {
    getByIdResponse = http.get(`${baseUrl}/orders/${orderId}`, {
      responseCallback: http.expectedStatuses(200, 404)
    });
    if (getByIdResponse.status === 200) {
      break;
    }

    sleep(0.5);
  }

  check(getByIdResponse, {
    'GET /orders/{id} status is 200': (r) => r.status === 200
  });

  sleep(1);
}
