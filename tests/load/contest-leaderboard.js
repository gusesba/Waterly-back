import http from "k6/http";
import { check } from "k6";

const baseUrl = __ENV.BASE_URL || "http://localhost:5004";
const contestId = __ENV.CONTEST_ID || "60000000-0000-0000-0000-000000000001";
const token = __ENV.ACCESS_TOKEN;

if (!token) throw new Error("ACCESS_TOKEN is required");

export const options = {
  vus: Number(__ENV.VUS || 25),
  duration: __ENV.DURATION || "5m",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<500"],
  },
};

export default function () {
  const page = (__VU + __ITER) % 20 + 1;
  const response = http.get(
    `${baseUrl}/api/v1/contests/${contestId}/leaderboard?page=${page}&pageSize=50`,
    { headers: { Authorization: `Bearer ${token}` } },
  );

  check(response, {
    "leaderboard returns 200": (result) => result.status === 200,
    "leaderboard is paginated": (result) => result.status === 200 && result.json("pageSize") === 50,
  });
}
