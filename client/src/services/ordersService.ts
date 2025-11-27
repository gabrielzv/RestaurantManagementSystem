import { api } from "./api";

export interface OrderItemReq {
  menuItemId?: number;
  name?: string;
  price: number;
  quantity: number;
  notes?: string;
}

export const ordersService = {
  async createOrder(payload: {
    restaurantId: number;
    waiterId?: number;
    tableNumber?: string;
    items?: OrderItemReq[];
  }) {
    const resp = await api.post("/orders", payload);
    return resp.data;
  },

  async replaceItems(orderId: number, items: OrderItemReq[]) {
    const resp = await api.put(`/orders/${orderId}/items`, items);
    return resp.data;
  },

  async confirmOrder(orderId: number) {
    const resp = await api.post(`/orders/${orderId}/confirm`);
    return resp.data;
  },

  async getOrder(orderId: number) {
    const resp = await api.get(`/orders/${orderId}`);
    return resp.data;
  },

  async getByAccessCode(accessCode: string) {
    const resp = await api.get(`/orders/byaccesscode/${encodeURIComponent(accessCode)}`);
    return resp.data;
  },

  async getByWaiter(waiterId: number) {
    const resp = await api.get(`/orders/bywaiter/${waiterId}`);
    return resp.data;
  },
};
