import http from 'k6/http';
import { check, sleep } from 'k6';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5214';

export const options = {
  scenarios: {
    create_orders_sustained_load: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 },
        { duration: '1m', target: 20 },
        { duration: '30s', target: 0 }
      ],
      gracefulRampDown: '10s'
    }
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<3000']
  }
};

export default function () {
  const payload = JSON.stringify({
    amount: 49.5,
    customerEmail: `load-${__VU}-${__ITER}-${Date.now()}@example.com`
  });

  const response = http.post(`${baseUrl}/orders`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'x-correlation-id': `k6-load-${__VU}-${__ITER}`
    }
  });

  check(response, {
    'POST /orders status is 201': (r) => r.status === 201,
    'POST /orders has orderId': (r) => {
      if (!r.body) return false;
      const parsed = JSON.parse(r.body);
      return typeof parsed.orderId === 'string' && parsed.orderId.length > 0;
    }
  });

  if (response.status !== 201 || !response.body) {
    return;
  }

  // Lightweight sampled validation to cover eventual read-model consistency under load.
  if (Math.random() > 0.2) {
    return;
  }

  const orderId = JSON.parse(response.body).orderId;
  let byIdResponse = null;
  for (let attempt = 0; attempt < 8; attempt += 1) {
    byIdResponse = http.get(`${baseUrl}/orders/${orderId}`, {
      responseCallback: http.expectedStatuses(200, 404)
    });

    if (byIdResponse.status === 200) {
      break;
    }

    sleep(0.4);
  }

  check(byIdResponse, {
    'Sampled GET /orders/{id} eventually 200': (r) => r.status === 200
  });
}
