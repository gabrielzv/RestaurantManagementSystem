<script setup lang="ts">
import { ref, onMounted, onActivated, onBeforeUnmount, onUnmounted } from "vue";
import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useRoute, useRouter } from "vue-router";
import {
  clearAccessCode,
  getNotifications,
  markNotificationAsRead,
} from "@/services/accessCodeService";
import { menuItemService } from "@/services/api";
import { ordersService } from "@/services/ordersService";
import type { OrderItemReq } from "@/services/ordersService";
import { orderSession } from "@/services/orderSession";
import { notificationService } from "@/services/notificationService";
import type { OrderNotification } from "@/services/notificationService";

const route = useRoute();
const router = useRouter();

const tableNumber = ref(route.params.tableNumber as string);
const waiterId = ref(Number(route.params.waiterId as string));
const accessCode = ref(route.params.code as string);
const loading = ref(false);
const error = ref("");
const successMessage = ref("");
const billing = ref(false);
let billingTimeout: number | undefined;

const sessionRaw =
  typeof localStorage !== "undefined" ? localStorage.getItem("waiter_session") : null;
const session = sessionRaw ? JSON.parse(sessionRaw) : null;
const restaurantId = session?.restaurantId ?? 1;

// Data types
interface MenuItem {
  id?: number;
  name: string;
  description?: string;
  price: number;
}
interface ClientNotification {
  id: number;
  tableNumber: string;
  message: string;
  createdAt: string;
}

const menu = ref<MenuItem[]>([]);
const cart = ref<
  Array<{ menuItemId?: number; name: string; price: number; quantity: number; notes?: string }>
>([]);
const currentOrderId = ref<number | null>(null);
const orderStatus = ref<string | null>(null);

// Notifications types
const clientNotifications = ref<ClientNotification[]>([]);
const orderNotifications = ref<OrderNotification[]>([]);
let notificationInterval: number | undefined;

const addOrderNotification = (msg: string, meta?: { orderId?: number; tableNumber?: string }) => {
  const tbl = meta?.tableNumber ?? tableNumber.value;
  notificationService.add(waiterId.value || undefined, tbl, msg, meta);
};

const orderStatusClass = (s: string | null) => {
  if (!s) return "status-other";
  const st = String(s).toLowerCase();
  if (st === "ready") return "status-ready";
  if (st === "sent") return "status-sent";
  return "status-other";
};

// Get human-readable status label in spanish
const getStatusLabel = (s: string | null) => {
  if (!s) return "Pendiente";
  const st = String(s).toLowerCase();
  if (st === "ready") return "Listo";
  if (st === "sent") return "Enviado";
  // fallback: capitalize first letter
  const trimmed = String(s).trim();
  return trimmed.charAt(0).toUpperCase() + trimmed.slice(1);
};

const formatCurrency = (value: number) =>
  new Intl.NumberFormat("es-CR", {
    style: "currency",
    currency: "CRC",
    minimumFractionDigits: 2,
  }).format(value);

const loadClientNotifications = async () => {
  try {
    const data = await getNotifications(accessCode.value);
    const normalized: ClientNotification[] = (data ?? []).map((item: unknown) => {
      const it = item as Partial<ClientNotification>;
      return {
        id: Number(it.id ?? 0),
        tableNumber: String(it.tableNumber ?? ""),
        message: String(it.message ?? ""),
        createdAt: String(it.createdAt ?? ""),
      };
    });
    for (const n of normalized)
      if (!clientNotifications.value.some((c) => c.id === n.id))
        clientNotifications.value.unshift(n);
  } catch (e) {
    console.error("Error loading client notifications", e);
  }
};

const dismissNotification = async (n: ClientNotification) => {
  try {
    await markNotificationAsRead(n.id);
    clientNotifications.value = clientNotifications.value.filter((c) => c.id !== n.id);
  } catch (e) {
    console.error("Error dismissing notification", e);
  }
};

// SignalR for order notifications
let hubConnection: HubConnection | null = null;
const initHub = async () => {
  try {
    const apiBase = import.meta.env.VITE_API_URL || "http://localhost:5000/api";
    const hubBase = apiBase.replace(/\/api\/?$/, "");
    const hubUrl = `${hubBase}/orderHub`;
    hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, { withCredentials: true })
      .configureLogging(LogLevel.None)
      .build();
    hubConnection.on("OrderReady", async (orderId: number) => {
      try {
        const data = await ordersService.getOrder(orderId);
        const tableNum =
          data.order?.TableNumber ??
          data.Order?.TableNumber ??
          data.tableNumber ??
          data.TableNumber ??
          data.table ??
          data.Table ??
          null;
        const msg = tableNum
          ? `Pedido de la mesa ${tableNum} ya está listo`
          : `Pedido ${orderId} ya está listo`;
        notificationService.add(waiterId.value || undefined, tableNum ?? tableNumber.value, msg, {
          orderId,
          tableNumber: tableNum,
        });
        if (currentOrderId.value === orderId) {
          try {
            const st = data.order?.Status ?? data.Order?.Status ?? data.status ?? data.Status;
            orderStatus.value = st;
          } catch (e) {
            console.error(e);
          }
        }
      } catch (e) {
        // fallback
        notificationService.add(
          waiterId.value || undefined,
          tableNumber.value,
          `Pedido ${orderId} listo (SignalR).`,
          { orderId },
        );
        console.error("Error fetching order data for notification", e);
      }
    });
    await hubConnection.start();
    if (waiterId.value) {
      try {
        await hubConnection.invoke("JoinGroup", waiterId.value.toString());
      } catch (e) {
        console.warn("JoinGroup failed", e);
      }
    }
  } catch (e) {
    console.warn("SignalR init failed", e);
  }
};

const stopHub = async () => {
  try {
    if (hubConnection) {
      if (waiterId.value) {
        try {
          await hubConnection.invoke("LeaveGroup", waiterId.value.toString());
        } catch {}
      }
      await hubConnection.stop();
      hubConnection = null;
    }
  } catch (e) {
    console.warn("SignalR stop failed", e);
  }
};

const loadMenu = async () => {
  try {
    const items = await menuItemService.getByRestaurant(restaurantId);
    menu.value = items;
  } catch (e) {
    console.error(e);
  }
};

const addToCart = (item: MenuItem) => {
  const id = item?.id ?? null;
  const existing = id ? cart.value.find((c) => c.menuItemId === id) : undefined;
  if (existing) existing.quantity += 1;
  else
    cart.value.push({
      menuItemId: id ?? undefined,
      name: item.name,
      price: item.price,
      quantity: 1,
    });
};

const decreaseItem = (item: MenuItem) => {
  const id = item?.id ?? null;
  if (!id) return;
  const idx = cart.value.findIndex((c) => c.menuItemId === id);
  if (idx === -1) return;
  const it = cart.value[idx];
  if (!it) return;
  if (it.quantity > 1) it.quantity -= 1;
  else cart.value.splice(idx, 1);
};

const getCartQty = (menuItemId: number | undefined | null) => {
  if (!menuItemId) return 0;
  const found = cart.value.find((c) => c.menuItemId === menuItemId);
  return found ? found.quantity : 0;
};

const createOrder = async () => {
  successMessage.value = "";
  if (cart.value.length === 0) {
    error.value = "El pedido está vacío";
    return;
  }
  loading.value = true;
  error.value = "";
  try {
    const items = cart.value.map<OrderItemReq>((c) => ({
      menuItemId: c.menuItemId,
      name: c.name,
      price: c.price,
      quantity: c.quantity,
      notes: c.notes,
    }));

    // If we already have a draft/current order, allow updating its items only while it's a draft.
    if (currentOrderId.value) {
      const isSent = orderStatus.value ? String(orderStatus.value).toLowerCase() === "sent" : false;
      if (isSent) {
        error.value = "El pedido ya fue enviado y no puede modificarse.";
        loading.value = false;
        return;
      }

      try {
        await ordersService.replaceItems(currentOrderId.value, items);
        addOrderNotification("Pedido actualizado (borrador).", {
          orderId: currentOrderId.value,
          tableNumber: tableNumber.value,
        });
      } catch (e) {
        console.error("Error updating order items", e);
        error.value = "Error al actualizar pedido en el servidor";
        loading.value = false;
        return;
      }
    } else {
      const payload = {
        restaurantId,
        waiterId: waiterId.value || undefined,
        tableNumber: tableNumber.value,
        items,
      };
      const res = await ordersService.createOrder(payload);
      currentOrderId.value = res.id ?? res.Id ?? null;
      addOrderNotification("Pedido creado en borrador. Puede confirmar cuando esté listo.", {
        orderId: currentOrderId.value ?? undefined,
        tableNumber: tableNumber.value,
      });
    }

    try {
      orderSession.save(waiterId.value || undefined, tableNumber.value, {
        orderId: currentOrderId.value as number,
        waiterId: waiterId.value,
        tableNumber: tableNumber.value,
        cart: cart.value,
        status: orderStatus.value ?? null,
      });
    } catch (e) {
      console.warn("orderSession.save failed", e);
    }
  } catch (e) {
    console.error(e);
    error.value = "Error al crear pedido";
  } finally {
    loading.value = false;
  }
};

const confirmOrder = async () => {
  successMessage.value = "";
  if (!currentOrderId.value) {
    error.value = "No hay pedido para confirmar";
    return;
  }
  loading.value = true;
  error.value = "";
  try {
    await ordersService.confirmOrder(currentOrderId.value);
    addOrderNotification("Pedido confirmado y enviado a cocina. Esperando preparación...", {
      orderId: currentOrderId.value ?? undefined,
      tableNumber: tableNumber.value,
    });
    orderStatus.value = "Sent";
    orderSession.updateStatus(waiterId.value || undefined, tableNumber.value, "Sent");
    const poll = setInterval(async () => {
      try {
        const data = await ordersService.getOrder(currentOrderId.value!);
        const st =
          data.order?.Status ??
          data.Order?.Status ??
          data.order?.status ??
          data.Order?.status ??
          data.Order?.Status;
        const normalized = st ?? data.Order?.Status ?? data.order?.Status;
        orderStatus.value = normalized as string | null;
        orderSession.updateStatus(
          waiterId.value || undefined,
          tableNumber.value,
          String(normalized),
        );
        if (normalized == "Ready" || normalized == "ready") {
          clearInterval(poll);
        }
      } catch (e) {
        console.error(e);
      }
    }, 3000);
  } catch (e) {
    console.error(e);
    error.value = "Error al confirmar pedido";
  } finally {
    loading.value = false;
  }
};

const clearTable = async (options?: { preserveMessage?: boolean }) => {
  if (!accessCode.value) {
    error.value = "No se encontró el código de acceso";
    return;
  }
  const preserveMessage = options?.preserveMessage ?? false;
  if (billingTimeout) {
    window.clearTimeout(billingTimeout);
    billingTimeout = undefined;
  }
  if (!preserveMessage) successMessage.value = "";
  billing.value = false;
  loading.value = true;
  error.value = "";
  try {
    await clearAccessCode(accessCode.value);
    try {
      orderSession.remove(waiterId.value || undefined, tableNumber.value);
      try {
        // Clear in-memory/local notifications for this waiter+table and global for this table
        notificationService.clear(waiterId.value || undefined, tableNumber.value);
      } catch(e) {
        console.warn("Error clearing notifications:", e);
      }
      try {
        notificationService.clear(undefined, tableNumber.value);
      } catch(e) {
        console.warn("Error clearing notifications:", e);
      }
      try {
        // If there's a client access_session in this browser that matches, remove it
        const as = localStorage.getItem("access_session");
        if (as) {
          try {
            const parsed = JSON.parse(as);
            if (parsed && parsed.code === accessCode.value)
              localStorage.removeItem("access_session");
          } catch(e) {
            console.warn("Error parsing access_session:", e);
          }
        }
      } catch(e) {
        console.warn("Error removing access_session:", e);
      }
    } catch (e) {
      console.warn("orderSession.remove failed", e);
    }
    router.push({ name: "waiter-panel" });
  } catch (e) {
    console.error(e);
    error.value = "Error al desocupar mesa";
  } finally {
    loading.value = false;
  }
};

const billTable = async () => {
  if (billing.value) return;
  if (!accessCode.value) {
    error.value = "No se encontró el código de acceso";
    return;
  }
  if (billingTimeout) {
    window.clearTimeout(billingTimeout);
    billingTimeout = undefined;
  }
  billing.value = true;
  error.value = "";
  successMessage.value = "";
  try {
    const data = await ordersService.getByAccessCode(accessCode.value);
    const ordersRaw = Array.isArray(data?.orders)
      ? data.orders
      : Array.isArray(data?.Orders)
        ? data.Orders
        : [];
    let subtotal = Number(data?.subtotal ?? data?.Subtotal ?? NaN);
    if (!Number.isFinite(subtotal)) {
      subtotal = ordersRaw.reduce((acc: number, order: any) => {
        const total = Number(order?.total ?? order?.Total ?? 0);
        return acc + (Number.isFinite(total) ? total : 0);
      }, 0);
    }
    if (!Number.isFinite(subtotal)) subtotal = 0;
    const formattedSubtotal = formatCurrency(subtotal);
    successMessage.value = `El subtotal a cobrar es ${formattedSubtotal}. Desocupando mesa en 5 segundos...`;
    billingTimeout = window.setTimeout(async () => {
      try {
        await clearTable({ preserveMessage: true });
      } finally {
        billing.value = false;
        billingTimeout = undefined;
      }
    }, 5000);
  } catch (e) {
    console.error("Error al calcular subtotal", e);
    error.value = "Error al calcular subtotal de la mesa";
    billing.value = false;
  }
};

const backToPanel = () => router.push({ name: "waiter-panel" });

onMounted(async () => {
  loadMenu();
  initHub();
  try {
    const s = orderSession.load(waiterId.value || undefined, tableNumber.value);
    const sessionExists = !!s;
    if (s) {
      // Verify that the server still has orders for this access code.
      // If the code/table was cleared, remove stale local session.
      try {
        const resp = await ordersService.getByAccessCode(accessCode.value);
        const ordersRaw = Array.isArray(resp?.orders)
          ? resp.orders
          : Array.isArray(resp?.Orders)
            ? resp.Orders
            : [];
        if (
          (!ordersRaw || ordersRaw.length === 0) &&
          !s.orderId &&
          (!s.cart || s.cart.length === 0)
        ) {
          try {
            orderSession.remove(waiterId.value || undefined, tableNumber.value);
            // clear local vars so UI starts fresh
            currentOrderId.value = null;
            cart.value = [];
            orderStatus.value = null;
            // Mark sessionExists false so notifications are cleared below
          } catch (e) {
            console.warn("orderSession.remove failed", e);
          }
        }
      } catch (e) {
        console.warn("Network error while verifying orders for access code:", e);
      }
      currentOrderId.value = s.orderId ?? null;
      if (s.cart && Array.isArray(s.cart) && s.cart.length > 0)
        cart.value = s.cart as typeof cart.value;
      orderStatus.value = s.status ?? null;
      if (currentOrderId.value) {
        ordersService
          .getOrder(currentOrderId.value)
          .then((data) => {
            const st = data.order?.Status ?? data.Order?.Status ?? data.status ?? data.Status;
            if (st) {
              orderStatus.value = st;
              orderSession.updateStatus(waiterId.value || undefined, tableNumber.value, String(st));
            }
          })
          .catch(() => {});
      }
    }
    // If there was no saved session for this waiter+table, clear previous order notifications
    if (!sessionExists) {
      try {
        notificationService.clear(waiterId.value || undefined, tableNumber.value);
      } catch (err) {
        console.warn("notificationService.clear failed", err);
      }
    }
  } catch (e) {
    console.warn("orderSession.load failed", e);
  }
  loadClientNotifications();
  notificationInterval = window.setInterval(loadClientNotifications, 3000);
  // Load persisted order notifications for this waiter+table
  try {
    orderNotifications.value = notificationService.get(
      waiterId.value || undefined,
      tableNumber.value,
    );
  } catch {
    orderNotifications.value = [];
  }
  // subscribe to new notifications
  const unsub = notificationService.subscribe(
    ({ waiterId: wid, tableNumber: tnum, notification }) => {
      if (
        (waiterId.value || undefined) === (wid || undefined) &&
        (tnum ?? tableNumber.value) === tableNumber.value
      ) {
        orderNotifications.value.unshift(notification);
      }
    },
  );
  // ensure we remove subscription on unmount
  onUnmounted(() => {
    try {
      unsub();
    } catch {}
  });
});

onActivated(() => loadMenu());
onBeforeUnmount(() => stopHub());
onUnmounted(() => {
  if (notificationInterval) window.clearInterval(notificationInterval);
  if (billingTimeout) {
    window.clearTimeout(billingTimeout);
    billingTimeout = undefined;
  }
});
</script>

<template>
  <main class="center">
    <div class="table-card">
      <h1>Atendiendo Mesa {{ tableNumber }}</h1>
      <p class="table-info">Mesero ID: {{ waiterId }} | Código: {{ accessCode }}</p>

      <div class="layout">
        <section class="menu">
          <h3 class="name">Menú</h3>
          <ul>
            <li
              v-for="item in menu"
              :key="item.id"
              :class="['menu-item', { selected: getCartQty(item.id) > 0 }]"
            >
              <div class="menu-main">
                <div>
                  <strong class="name">{{ item.name }}</strong>
                  <div class="desc">{{ item.description }}</div>
                  <div class="price">${{ item.price.toFixed(2) }}</div>
                </div>
                <div class="menu-actions">
                  <button class="small" @click.prevent="decreaseItem(item)" aria-label="Quitar">
                    -
                  </button>
                  <div class="name selected-badge-inline" v-if="getCartQty(item.id) > 0">
                    {{ getCartQty(item.id) }}
                  </div>
                  <button class="small" @click.prevent="addToCart(item)" aria-label="Añadir">
                    +
                  </button>
                </div>
              </div>
            </li>
          </ul>
        </section>

        <section class="cart">
          <h3 class="name">Pedido</h3>
          <ul>
            <li v-for="(c, idx) in cart" :key="idx">
              <div class="cart-row">
                <div class="name">{{ c.name }}</div>
                <div class="name qty">{{ c.quantity }}</div>
                <div class="price">${{ (c.price * c.quantity).toFixed(2) }}</div>
              </div>
            </li>
          </ul>
          <div class="name" v-if="cart.length === 0">No se han agregado items al pedido.</div>
          <div class="cart-actions">
            <button
              class="create-order"
              @click="createOrder"
              :disabled="
                loading || (orderStatus ? String(orderStatus).toLowerCase() === 'sent' : false)
              "
            >
              {{
                loading
                  ? currentOrderId
                    ? "Actualizando..."
                    : "Creando..."
                  : currentOrderId
                    ? "Actualizar Pedido"
                    : "Crear Pedido"
              }}
            </button>
            <button
              class="confirm-order"
              @click="confirmOrder"
              :disabled="!currentOrderId || loading"
            >
              Enviar Pedido
            </button>
          </div>
        </section>
      </div>

      <div class="notifications">
        <h3>Notificaciones (Clientes)</h3>
        <ul v-if="clientNotifications.length > 0">
          <li v-for="notif in clientNotifications" :key="notif.id" class="notification-item">
            <div class="notification-content">
              <p class="name">{{ notif.message }}</p>
            </div>
            <button @click="dismissNotification(notif)" class="dismiss-btn">✓</button>
          </li>
        </ul>
        <p v-else class="no-notifications">No hay notificaciones</p>
      </div>

      <div class="notifications">
        <h3>Estado</h3>
        <div v-if="orderStatus" :class="['order-status', orderStatusClass(orderStatus)]">
          Estado: {{ getStatusLabel(orderStatus) }}
        </div>
        <p v-else class="no-notifications">No se ha realizado ningún pedido</p>
      </div>

      <div class="notifications">
        <h3>Notificaciones (Órdenes)</h3>
        <ul v-if="orderNotifications.length > 0">
          <li v-for="notif in orderNotifications" :key="notif.id" class="notification-item">
            <div class="notification-content">
              <p class="name">{{ notif.message }}</p>
            </div>
          </li>
        </ul>
        <p v-else class="no-notifications">No hay notificaciones</p>
      </div>

      <div class="actions">
        <button @click="billTable" :disabled="loading || billing" class="bill-btn">
          {{ billing ? "Preparando cobro..." : "Cobrar Mesa" }}
        </button>
        <button @click="clearTable()" :disabled="loading || billing" class="clear-btn">
          {{ loading ? "Desocupando..." : "Desocupar Mesa" }}
        </button>
        <button @click="backToPanel" class="secondary">Volver al Panel</button>
      </div>

      <p class="success" v-if="successMessage">{{ successMessage }}</p>
      <p class="error" v-if="error">{{ error }}</p>
    </div>
  </main>
</template>

<style scoped>
.center {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  padding: 2rem;
}
.table-card {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 1100px;
}
.layout {
  display: block;
  gap: 1rem;
}
.name,
.price,
.desc {
  color: black;
}
h1 {
  text-align: center;
  margin-bottom: 1rem;
  color: #333;
}
.table-info {
  text-align: center;
  color: #666;
  margin-bottom: 2rem;
}
.notifications {
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 2rem;
}
.notifications h3 {
  text-align: center;
  margin-bottom: 1rem;
  color: #333;
}
.notifications ul {
  list-style: none;
  padding: 0;
  margin: 0;
}
.notification-item {
  background: white;
  padding: 1rem;
  margin-bottom: 0.75rem;
  border-radius: 6px;
  border-left: 4px solid #667eea;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}
.notification-content {
  flex: 1;
}
.dismiss-btn {
  background: #4caf50;
  color: white;
  border: none;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
.no-notifications {
  text-align: center;
  color: #666;
  font-style: italic;
  margin: 1rem 0;
}
.actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
}
button {
  background: #dc3545;
  color: white;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: 4px;
  font-size: 1rem;
  cursor: pointer;
}
.bill-btn {
  background: #ff9800;
}
.create-order {
  background: #28a745;
}
.confirm-order {
  background: #007bff;
}
button:disabled {
  background: #ccc;
  cursor: not-allowed;
}
.clear-btn {
  background: #dc3545;
}
.secondary {
  background: #6c757d;
}
.success {
  color: #155724;
  margin-top: 1rem;
  text-align: center;
  background: #d4edda;
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid #c3e6cb;
}
.error {
  color: #dc3545;
  margin-top: 1rem;
  text-align: center;
  background: #f8d7da;
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid #f5c6cb;
}
.menu-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem;
  border-radius: 6px;
  margin-bottom: 0.5rem;
  min-height: 72px;
}

.menu {
  padding: 0 0.6rem;
  box-sizing: border-box;
}
.menu-item {
  box-sizing: border-box;
  padding: 0.9rem;
}
.menu-main .desc,
.menu-main .name,
.menu-main strong {
  white-space: normal;
  word-break: break-word;
  overflow-wrap: anywhere;
}
.menu ul {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.5rem;
  padding: 0;
  list-style: none;
}
@media (min-width: 1024px) {
  .menu ul {
    grid-template-columns: repeat(2, 1fr);
  }
}
.menu-item.selected {
  border: 2px solid var(--color-accent);
}
.menu-main {
  display: flex;
  gap: 1rem;
  align-items: center;
  width: 100%;
  justify-content: space-between;
}
.selected-badge-inline {
  min-width: 28px;
  text-align: center;
  border-radius: 6px;
  color: var(--color-accent);
  font-weight: 700;
  padding: 2px 6px;
  margin: 0 6px;
}
.menu-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  width: 96px;
  flex-shrink: 0;
}
.menu-actions .small {
  background: #fff;
  color: #000;
  border: 1px solid #ddd;
  width: 32px;
  height: 32px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  cursor: pointer;
  padding: 0;
}
.cart .cart-row {
  display: flex;
  gap: 1rem;
  align-items: center;
  justify-content: space-between;
  padding: 0.4rem 0.2rem;
}
.cart ul {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.5rem;
  padding: 0;
  list-style: none;
}
@media (min-width: 1024px) {
  .cart ul {
    grid-template-columns: repeat(2, 1fr);
  }
}
.cart-actions {
  display: flex;
  justify-content: center;
  gap: 1rem;
  margin-top: 1rem;
  margin-bottom: 1.5rem;
  padding: 0 0.75rem;
}
.cart-actions button {
  min-width: 160px;
  padding: 0.6rem 1rem;
}
@media (max-width: 480px) {
  .menu-actions {
    width: 84px;
  }
  .menu-actions .small {
    width: 28px;
    height: 28px;
  }
  .cart-actions button {
    min-width: 100px;
  }
}

@media (max-width: 700px) {
  .panel-card {
    padding-left: 1rem;
    padding-right: 1rem;
  }
  .menu {
    padding-left: 0.75rem;
    padding-right: 0.75rem;
  }
  .cart-actions {
    padding-left: 0.1rem;
    padding-right: 0.1rem;
  }
  .menu-item {
    padding: 0.7rem;
  }
}

.order-status {
  display: inline-block;
  padding: 0.4rem 0.8rem;
  border-radius: 8px;
  font-weight: 700;
  color: #fff;
  text-align: center;
}
.status-ready {
  background: #28a745;
}
.status-sent {
  background: #007bff;
}
.status-other {
  background: #6c757d;
}

.menu,
.cart {
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 1.25rem;
  box-sizing: border-box;
}

.menu {
  padding-left: 0.6rem;
  padding-right: 0.6rem;
}
.cart {
  padding-left: 0.75rem;
  padding-right: 0.75rem;
}

.menu .name,
.cart .name {
  text-align: center;
  margin-bottom: 1rem;
}

.notifications .order-status {
  display: block;
  width: max-content;
  margin: 0.5rem auto;
}
</style>
