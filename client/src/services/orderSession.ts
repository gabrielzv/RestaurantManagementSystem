export interface OrderSession {
  orderId: number | null
  waiterId?: number
  tableNumber?: string
  cart?: Array<{ menuItemId?: number; name?: string; price: number; quantity: number; notes?: string }>
  status?: string | null
}

const keyFor = (waiterId?: number, tableNumber?: string) => {
  if (!waiterId || !tableNumber) return `order_session_global`;
  return `order_session_${waiterId}_${tableNumber}`;
}

export const orderSession = {
  save(waiterId: number | undefined, tableNumber: string | undefined, session: OrderSession) {
    try {
      const k = keyFor(waiterId, tableNumber);
      localStorage.setItem(k, JSON.stringify(session));
    } catch (e) {
        // Ignore for now
    }
  },

  load(waiterId: number | undefined, tableNumber: string | undefined): OrderSession | null {
    try {
      const k = keyFor(waiterId, tableNumber);
      const raw = localStorage.getItem(k);
      if (!raw) return null;
      return JSON.parse(raw) as OrderSession;
    } catch (e) {
      return null;
    }
  },

  remove(waiterId: number | undefined, tableNumber: string | undefined) {
    try {
      const k = keyFor(waiterId, tableNumber);
      localStorage.removeItem(k);
    } catch (e) {
      // ignore
    }
  },

  updateStatus(waiterId: number | undefined, tableNumber: string | undefined, status: string) {
    try {
      const s = this.load(waiterId, tableNumber);
      if (!s) return;
      s.status = status;
      this.save(waiterId, tableNumber, s);
    } catch (e) {
      // ignore
    }
  }
}

export default orderSession;
