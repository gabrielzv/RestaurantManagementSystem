import { api } from "./api";

export interface CreateCodeRequest {
  restaurantId: number;
  tableNumber?: string;
  waiterId?: number;
  ttlMinutes?: number;
}

export async function createAccessCode(req: CreateCodeRequest) {
  const resp = await api.post("/accesscodes", req);
  return resp.data;
}

export async function validateAccessCode(code: string) {
  const resp = await api.get(`/accesscodes/validate/${encodeURIComponent(code)}`);
  return resp.data;
}

export async function getAccessCodesByWaiter(waiterId: number) {
  const resp = await api.get(`/accesscodes/bywaiter/${waiterId}`);
  return resp.data;
}
