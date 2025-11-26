export interface OrderNotification {
  id: string;
  message: string;
  createdAt: string;
  orderId?: number;
  tableNumber?: string;
}

const emitter = new EventTarget();

// Utility to create a unique key for storing notifications
const keyFor = (waiterId?: number, tableNumber?: string) =>
  `order_notifications_${waiterId ?? "global"}_${tableNumber ?? "all"}`;

export const notificationService = {
  get(waiterId?: number, tableNumber?: string): OrderNotification[] {
    try {
      const k = keyFor(waiterId, tableNumber);
      const raw = localStorage.getItem(k);
      if (!raw) return [];
      return JSON.parse(raw) as OrderNotification[];
    } catch (err) {
      console.warn("notificationService.get failed", err);
      return [];
    }
  },

  // Add a new notification and notify subscribers
  add(
    waiterId: number | undefined,
    tableNumber: string | undefined,
    message: string,
    meta?: { orderId?: number; tableNumber?: string },
  ): OrderNotification | null {
    try {
      const k = keyFor(waiterId, tableNumber);
      const list = this.get(waiterId, tableNumber);
      const n: OrderNotification = {
        id: `${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
        message,
        createdAt: new Date().toISOString(),
        orderId: meta?.orderId,
        tableNumber: meta?.tableNumber ?? tableNumber,
      };
      list.unshift(n);
      localStorage.setItem(k, JSON.stringify(list));
      emitter.dispatchEvent(
        new CustomEvent("notification", { detail: { waiterId, tableNumber, notification: n } }),
      );
      return n;
    } catch (e) {
      console.warn("notificationService.add failed", e);
      return null;
    }
  },

  // Clear notifications for a specific waiter and table
  clear(waiterId?: number, tableNumber?: string) {
    try {
      const k = keyFor(waiterId, tableNumber);
      localStorage.removeItem(k);
      emitter.dispatchEvent(
        new CustomEvent("notification:cleared", { detail: { waiterId, tableNumber } }),
      );
    } catch (err) {
      console.warn("notificationService.clear failed", err);
    }
  },

  // Subscribe to new notifications
  subscribe(
    handler: (payload: {
      waiterId?: number;
      tableNumber?: string;
      notification: OrderNotification;
    }) => void,
  ) {
    const h = (e: Event) => {
      const ce = e as CustomEvent;
      handler(ce.detail);
    };
    emitter.addEventListener("notification", h);
    return () => emitter.removeEventListener("notification", h);
  },
};

export default notificationService;
