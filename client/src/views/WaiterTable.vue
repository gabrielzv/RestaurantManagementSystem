<script setup lang="ts">
import { ref, onMounted, onActivated, onBeforeUnmount } from "vue";
import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useRoute, useRouter } from "vue-router";
import { clearAccessCode } from "@/services/accessCodeService";
import { menuItemService } from "@/services/api";
import { ordersService } from "@/services/ordersService";
import type { OrderItemReq } from "@/services/ordersService";
import { orderSession } from "@/services/orderSession";

const route = useRoute();
const router = useRouter();

const tableNumber = ref(route.params.tableNumber as string);
const waiterId = ref(Number(route.params.waiterId as string));
const accessCode = ref(route.params.code as string);
const loading = ref(false);
const error = ref("");

// Notifications
const notifications = ref<string[]>([]);
const addNotification = (msg: string) => notifications.value.push(msg);

// SignalR hub connection (for waiter realtime notifications)
let hubConnection: HubConnection | null = null;

// Order status updates via SignalR
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
      addNotification(`Pedido ${orderId} listo (SignalR).`);
      if (currentOrderId.value === orderId) {
        try {
          const data = await ordersService.getOrder(orderId);
          const st = data.order?.Status ?? data.Order?.Status ?? data.status ?? data.Status;
          orderStatus.value = st;
        } catch (e) {
          console.error(e);
        }
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
  } catch (err) {
    console.warn("SignalR init failed", err);
  }
};

// Stop SignalR hub
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

// Menu and cart
interface MenuItem {
  id?: number;
  name: string;
  description?: string;
  price: number;
}

const menu = ref<MenuItem[]>([]);
const cart = ref<
  Array<{ menuItemId?: number; name: string; price: number; quantity: number; notes?: string }>
>([]);
const currentOrderId = ref<number | null>(null);
const orderStatus = ref<string | null>(null);

// Session + restaurant
const sessionRaw =
  typeof localStorage !== "undefined" ? localStorage.getItem("waiter_session") : null;
const session = sessionRaw ? JSON.parse(sessionRaw) : null;
const restaurantId = session?.restaurantId ?? 1;

const loadMenu = async () => {
  try {
    // Use session restaurantId when available
    const items = await menuItemService.getByRestaurant(restaurantId);
    menu.value = items;
  } catch (err) {
    console.error(err);
  }
};

// Cart manipulation
const addToCart = (item: MenuItem) => {
  const id = item?.id ?? null;
  const existing = id ? cart.value.find((c) => c.menuItemId === id) : undefined;
  if (existing) {
    existing.quantity += 1;
  } else {
    cart.value.push({
      menuItemId: id ?? undefined,
      name: item.name,
      price: item.price,
      quantity: 1,
    });
  }
};

// Increase/decrease item quantity in cart
const increaseItem = (item: MenuItem) => {
  addToCart(item);
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

// Get quantity of item in cart
const getCartQty = (menuItemId: number | undefined | null) => {
  if (!menuItemId) return 0;
  const found = cart.value.find((c) => c.menuItemId === menuItemId);
  return found ? found.quantity : 0;
};

// Create order
const createOrder = async () => {
  if (cart.value.length === 0) {
    error.value = "El pedido está vacío";
    return;
  }
  loading.value = true;
  error.value = "";
  try {
    const payload = {
      restaurantId: restaurantId,
      waiterId: waiterId.value || undefined,
      tableNumber: tableNumber.value,
      items: cart.value.map<OrderItemReq>((c) => ({
        menuItemId: c.menuItemId,
        name: c.name,
        price: c.price,
        quantity: c.quantity,
        notes: c.notes,
      })),
    };
    const res = await ordersService.createOrder(payload);
    currentOrderId.value = res.id ?? res.Id ?? res.Id;
    addNotification("Pedido creado en borrador. Puede modificar antes de confirmar.");
    // Persist the created order so it survives a refresh
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
  } catch (err) {
    console.error(err);
    error.value = "Error al crear pedido";
  } finally {
    loading.value = false;
  }
};

// Confirm order
const confirmOrder = async () => {
  if (!currentOrderId.value) {
    error.value = "No hay pedido para confirmar";
    return;
  }
  loading.value = true;
  error.value = "";
  try {
    await ordersService.confirmOrder(currentOrderId.value);
    addNotification("Pedido confirmado y enviado a cocina. Esperando preparación...");
    orderStatus.value = "Sent";
    // update persisted status
    orderSession.updateStatus(waiterId.value || undefined, tableNumber.value, "Sent");
    // Poll for status until Ready
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
        orderStatus.value = normalized;
        // update persisted status each poll
        orderSession.updateStatus(
          waiterId.value || undefined,
          tableNumber.value,
          String(normalized),
        );
        if (normalized == "Ready" || normalized == "ready") {
          clearInterval(poll);
          addNotification("Pedido listo para servir (simulado).");
        }
      } catch (e) {
        console.error(e);
      }
    }, 3000);
  } catch (err) {
    console.error(err);
    error.value = "Error al confirmar pedido";
  } finally {
    loading.value = false;
  }
};

// Clear table
const clearTable = async () => {
  if (!accessCode.value) {
    error.value = "No se encontró el código de acceso";
    return;
  }
  loading.value = true;
  error.value = "";
  try {
    await clearAccessCode(accessCode.value);
    addNotification("¡Mesa desocupada exitosamente! El código ha sido borrado.");
    // remove persisted order session for this table
    try {
      orderSession.remove(waiterId.value || undefined, tableNumber.value);
    } catch (e) {
      console.warn("orderSession.remove failed", e);
    }
  } catch (err: unknown) {
    error.value = "Error al desocupar mesa";
    console.error(err);
  } finally {
    loading.value = false;
  }
};

// Navigate back to waiter panel
const backToPanel = () => {
  router.push({ name: "waiter-panel" });
};

onMounted(() => {
  loadMenu();
  // start SignalR for waiter notifications
  initHub();
  // Try to rehydrate in-progress order from localStorage
  try {
    const s = orderSession.load(waiterId.value || undefined, tableNumber.value);
    if (s) {
      currentOrderId.value = s.orderId ?? null;
      if (s.cart && Array.isArray(s.cart) && s.cart.length > 0) {
        cart.value = s.cart as typeof cart.value;
      }
      orderStatus.value = s.status ?? null;
      if (currentOrderId.value) {
        // attempt to refresh status from backend
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
  } catch (e) {
    // ignore
  }
});

onActivated(() => {
  loadMenu();
});

onBeforeUnmount(() => {
  stopHub();
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
                  <button class="small" @click.prevent="increaseItem(item)" aria-label="Añadir">
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
            <button class="create-order" @click="createOrder" :disabled="loading">
              Crear Pedido
            </button>
            <button
              class="confirm-order"
              @click="confirmOrder"
              :disabled="!currentOrderId || loading"
            >
              Enviar Pedido
            </button>
          </div>
          <div class="name" v-if="orderStatus">Estado: {{ orderStatus }}</div>
        </section>
      </div>

      <div class="notifications">
        <h3>Estado del Pedido</h3>
        <ul>
          <li class="name" v-for="(notif, index) in notifications" :key="index">{{ notif }}</li>
        </ul>
      </div>

      <div class="actions">
        <button @click="clearTable" :disabled="loading" class="clear-btn">
          {{ loading ? "Desocupando..." : "Desocupar Mesa" }}
        </button>
        <button @click="backToPanel" class="secondary">Volver al Panel</button>
      </div>

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

.notifications li {
  background: white;
  padding: 0.5rem;
  margin-bottom: 0.5rem;
  border-radius: 4px;
  border-left: 3px solid #007bff;
}

.notifications p {
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

.create-order {
  background: #28a745;
}

.confirm-order {
  background: #007bff;
}

button:hover:not(:disabled) {
  background: var(--color-accent);
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

.success-notification {
  background: #d4edda !important;
  border-left-color: #28a745 !important;
  color: #155724;
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

.selected-badge {
  border: 2px solid #dc3545;
  color: #dc3545;
  padding: 4px 8px;
  border-radius: 6px;
  font-weight: 700;
  margin-left: 0.5rem;
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
  color: #000000;
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

.menu-actions .small:active {
  transform: translateY(1px);
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

.menu-actions button {
  padding: 0.4rem 0.6rem;
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
    min-width: 120px;
  }
}
</style>
