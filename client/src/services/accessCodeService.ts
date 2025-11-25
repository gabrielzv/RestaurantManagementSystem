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

export async function clearAccessCode(code: string) {
  const resp = await api.delete(`/accesscodes/clear/${encodeURIComponent(code)}`);
  return resp.data;
}

export async function notifyWaiter(accessCode: string, message?: string): Promise<void> {
  await api.post("/accesscodes/notify", {
    accessCode,
    message,
  });
}

export async function getNotifications(accessCode: string): Promise<any[]> {
  const response = await api.get(`/accesscodes/notifications/${accessCode}`);
  return response.data;
}

export async function markNotificationAsRead(notificationId: number): Promise<void> {
  await api.post(`/accesscodes/notifications/markread/${notificationId}`);
}

export async function getAccessCodesByWaiter(waiterId: number) {
  const resp = await api.get(`/accesscodes/bywaiter/${waiterId}`);
  return resp.data;
}
