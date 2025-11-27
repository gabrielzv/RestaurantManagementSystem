<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from "vue";
import { useRouter } from "vue-router";
import { menuItemService, type MenuItem } from "@/services/api";
import HeroCarousel from "@/components/HeroCarousel.vue";
import { getRestaurantById } from "@/services/restaurantsService";
import { validateAccessCode, notifyWaiter } from "@/services/accessCodeService";
import { ordersService } from "@/services/ordersService";

const router = useRouter();

// Type extending MenuItem to include optional image property (In DB there is no image field)
type MenuItemWithImage = MenuItem & { image?: string };

const menuItems = ref<MenuItemWithImage[]>([]);
const loading = ref(true);
const error = ref("");
const selectedCategory = ref("Todo el menú");
const hasActiveSession = ref(false);
const callingWaiter = ref(false);
const callSuccess = ref(false);

interface ReceiptItem {
  name: string;
  quantity: number;
  price: number;
  lineTotal: number;
}

interface ReceiptOrder {
  id: number;
  status: string;
  createdAt?: string | null;
  items: ReceiptItem[];
  total: number;
}

const receiptOrders = ref<ReceiptOrder[]>([]);
const receiptSubtotal = ref(0);

const categories = ["Todo el menú", "Entradas", "Platos fuertes", "Bebidas", "Postres"];

const formatCurrency = (value: number) =>
  new Intl.NumberFormat("es-CR", {
    style: "currency",
    currency: "CRC",
    minimumFractionDigits: 2,
  }).format(value);

const getStatusLabel = (status: string) => {
  const normalized = (status || "").toLowerCase();
  if (normalized === "ready") return "Listo";
  if (normalized === "sent") return "Enviado";
  return status || "Pendiente";
};

const getStatusClass = (status: string) => {
  const normalized = (status || "").toLowerCase();
  if (normalized === "ready") return "ready";
  if (normalized === "sent") return "sent";
  return "pending";
};

const formatOrderTime = (value?: string | null) => {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
};

const formattedSubtotal = computed(() => formatCurrency(receiptSubtotal.value));
const hasReceipt = computed(() => receiptOrders.value.length > 0);

/* Load menu items from the API */
const loadMenuItems = async () => {
  try {
    loading.value = true;
    error.value = "";

    // If we have a session with restaurantId, load only that restaurant's menu items
    let sessionRestaurantId: number | null = null;
    try {
      const session = localStorage.getItem("access_session");
      if (session) {
        const s = JSON.parse(session);
        if (s && s.restaurantId) sessionRestaurantId = Number(s.restaurantId);
      }
    } catch {}

    if (sessionRestaurantId) {
      menuItems.value = await menuItemService.getByRestaurant(sessionRestaurantId);
    } else {
      menuItems.value = await menuItemService.getAll();
    }

    /* Load image map */
    let imageMap: Record<string, string> = {};
    try {
      const resp = await fetch("/images/menu/map.json");
      if (resp.ok) imageMap = await resp.json();
    } catch (e) {
      /* Ignore errors — we'll fallback to convention-based filenames */
      console.warn("image map not found or failed to load", e);
      imageMap = {};
    }

    /* This function normalizes strings to create URL-friendly slugs */
    const toSlug = (s: string) =>
      s
        .toString()
        .toLowerCase()
        .normalize("NFD")
        .replace(/\p{Diacritic}/gu, "")
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");

    /* Assign images to menu items */
    menuItems.value = menuItems.value.map((it, idx) => {
      const item = it as MenuItemWithImage;
      if (!item.image) {
        const slug = toSlug(item.name ?? String(item.id ?? "item" + idx));
        item.image = imageMap[slug] || `/images/menu/${slug}.svg` || "/images/menu/placeholder.svg";
      }
      return item;
    });

    /* Normalize category names to standard categories */
    const normalizeCategory = (c: string | undefined) => {
      if (!c) return null;
      const s = c.toString().toLowerCase();
      if (/drink|beverage|bebida|refresco|limonada|batido|jugo|cerveza|vino|agua|cafe/.test(s))
        return "Bebidas";
      if (/dessert|postre|tres leches|volcan|flan|cake|helado/.test(s)) return "Postres";
      if (/appetizer|starter|entrada|entradas|nacho|taco|ceviche|ensalada|entrada/.test(s))
        return "Entradas";
      if (
        /main|plato|principal|platos|casado|pechuga|pasta|lasaña|filete|carne|pollo|pescado/.test(s)
      )
        return "Platos fuertes";
      return null;
    };

    /* Guess category based on item name if not provided */
    const guessCategory = (name: string | undefined) => {
      const s = (name || "").toLowerCase();
      if (/ceviche|ensalada|entrada|nacho|taco|empanada|aperitivo/.test(s)) return "Entradas";
      if (/tres leches|volcan|flan|postre|pastel|cheesecake|helado|dulce/.test(s)) return "Postres";
      if (/refresco|limonada|batido|jugo|cerveza|vino|agua|cafe|té|te/.test(s)) return "Bebidas";
      return "Platos fuertes";
    };

    /* Assign normalized or guessed categories to menu items */
    menuItems.value = menuItems.value.map((it) => {
      const item = it as MenuItemWithImage;
      const normalized = normalizeCategory(item.category);
      item.category = normalized || guessCategory(item.name);
      return item;
    });
  } catch (err) {
    error.value = "Failed to load menu items. Make sure the API is running.";
    console.error("Error loading menu items:", err);
  } finally {
    loading.value = false;
  }
};

// Check if access code is still valid
const checkAccessCode = async () => {
  const session = localStorage.getItem("access_session");
  if (session) {
    try {
      const s = JSON.parse(session);
      if (s && s.code) {
        // Try to validate the code again
        await validateAccessCode(s.code);

        // If there are no orders, clear the local access session and receipt state
        try {
          const resp = await ordersService.getByAccessCode(s.code);
          const ordersRaw = Array.isArray(resp?.orders)
            ? resp.orders
            : Array.isArray(resp?.Orders)
              ? resp.Orders
              : [];
          if ((!ordersRaw || ordersRaw.length === 0) && receiptOrders.value.length > 0) {
            // Clear the client-side session and UI
            try {
              localStorage.removeItem("access_session");
            } catch {}
            try {
              localStorage.removeItem("restaurant_name");
              window.dispatchEvent(new CustomEvent("restaurant-name-changed", { detail: null }));
            } catch {}
            receiptOrders.value = [];
            receiptSubtotal.value = 0;
            hasActiveSession.value = false;
            alert("Su mesa ha sido desocupada. Por favor ingrese un nuevo código.");
            router.push({ name: "code" });
            return;
          }
        } catch (e) {
          console.warn("Error checking orders during access code validation:", e);
        }
      }
    } catch {
      // Code no longer valid, clear session and redirect
      localStorage.removeItem("access_session");
      try {
        localStorage.removeItem("restaurant_name");
        window.dispatchEvent(new CustomEvent("restaurant-name-changed", { detail: null }));
      } catch {}
      alert("Su mesa ha sido desocupada. Por favor ingrese un nuevo código.");
      router.push({ name: "code" });
    }
  }
};

const callWaiter = async () => {
  const session = localStorage.getItem("access_session");
  if (!session) return;

  try {
    const s = JSON.parse(session);
    if (s && s.code) {
      console.log("Llamando al mesero con código:", s.code);
      callingWaiter.value = true;
      callSuccess.value = false;
      await notifyWaiter(s.code);
      console.log("Notificación enviada exitosamente");
      callSuccess.value = true;
      setTimeout(() => {
        callSuccess.value = false;
      }, 3000);
    }
  } catch (err) {
    console.error("Error calling waiter:", err);
  } finally {
    callingWaiter.value = false;
  }
};

const loadReceipt = async () => {
  const session = localStorage.getItem("access_session");
  if (!session) {
    receiptOrders.value = [];
    receiptSubtotal.value = 0;
    return;
  }

  try {
    const parsed = JSON.parse(session);
    const code = parsed?.code;
    if (!code) {
      receiptOrders.value = [];
      receiptSubtotal.value = 0;
      return;
    }

    const data = await ordersService.getByAccessCode(code);
    const ordersRaw = data?.orders ?? data?.Orders ?? [];

    const normalizedOrders = (ordersRaw as any[]).map((order) => {
      const itemsSource = order?.items ?? order?.Items ?? [];
      const items: ReceiptItem[] = (itemsSource as any[]).map((item) => {
        const price = Number(item?.price ?? item?.Price ?? 0);
        const quantity = Number(item?.quantity ?? item?.Quantity ?? 0);
        const line = Number(item?.lineTotal ?? item?.LineTotal ?? price * quantity);
        return {
          name: item?.name ?? item?.Name ?? "Artículo",
          quantity: Number.isFinite(quantity) ? quantity : 0,
          price: Number.isFinite(price) ? price : 0,
          lineTotal: Number.isFinite(line)
            ? line
            : (Number.isFinite(price) ? price : 0) * (Number.isFinite(quantity) ? quantity : 0),
        };
      });

      const fallbackTotal = items.reduce((acc, it) => acc + it.lineTotal, 0);
      const orderTotal = Number(order?.total ?? order?.Total ?? fallbackTotal);

      return {
        id: Number(order?.id ?? order?.Id ?? 0),
        status: order?.status ?? order?.Status ?? "Sent",
        createdAt: order?.createdAt ?? order?.CreatedAt ?? null,
        items,
        total: Number.isFinite(orderTotal) ? orderTotal : fallbackTotal,
      } as ReceiptOrder;
    });

    receiptOrders.value = normalizedOrders.filter((order) => order.id > 0);

    // Clear local session and UI so the guest doesn't see a past order for a cleared table.
    try {
      const serverCount = Array.isArray(ordersRaw) ? ordersRaw.length : 0;
      if (serverCount === 0 && receiptOrders.value.length > 0) {
        try {
          localStorage.removeItem("access_session");
        } catch {}
        try {
          localStorage.removeItem("restaurant_name");
          window.dispatchEvent(new CustomEvent("restaurant-name-changed", { detail: null }));
        } catch {}
        receiptOrders.value = [];
        receiptSubtotal.value = 0;
        hasActiveSession.value = false;
        alert("Su mesa ha sido desocupada. Por favor ingrese un nuevo código.");
        router.push({ name: "code" });
        return;
      }
    } catch (e) {
      console.warn("Error during server-empty-orders cleanup check:", e);
    }

    const subtotalRaw = Number(
      data?.subtotal ??
        data?.Subtotal ??
        receiptOrders.value.reduce((acc, it) => acc + it.total, 0),
    );
    receiptSubtotal.value = Number.isFinite(subtotalRaw) ? subtotalRaw : 0;
  } catch (err) {
    console.error("Error loading receipt:", err);
  }
};

let checkInterval: number | undefined;

const onStorage = (e: StorageEvent) => {
  try {
    if (e.key === "access_session" && e.newValue === null) {
      // Clear UI and redirect home (/)
      try {
        localStorage.removeItem("access_session");
      } catch {}
      try {
        localStorage.removeItem("restaurant_name");
        window.dispatchEvent(new CustomEvent("restaurant-name-changed", { detail: null }));
      } catch {}
      receiptOrders.value = [];
      receiptSubtotal.value = 0;
      hasActiveSession.value = false;
      alert("Su mesa ha sido desocupada. Por favor ingrese un nuevo código.");
      router.push({ path: "/" });
    }
  } catch (err) {
    console.warn("storage event handler error", err);
  }
};

onMounted(() => {
  window.addEventListener("storage", onStorage);
  // Check if user has an active session
  const session = localStorage.getItem("access_session");
  hasActiveSession.value = !!session;
  loadMenuItems();
  // Check access code validity
  checkAccessCode();
  loadReceipt();
  // Poll every 5 seconds to check if code still exists
  const tick = () => {
    checkAccessCode();
    void loadReceipt();
  };
  checkInterval = window.setInterval(tick, 5000);

  // load session restaurant info if available
  if (session) {
    try {
      const s = JSON.parse(session);
      if (s && s.restaurantId) {
        getRestaurantById(s.restaurantId)
          .then((r) => {
            if (r && r.name) {
              // persist restaurant name for other parts of the app
              try {
                localStorage.setItem("restaurant_name", r.name);
              } catch {}

              // notify other parts of the app in this window that the restaurant name changed
              try {
                window.dispatchEvent(
                  new CustomEvent("restaurant-name-changed", { detail: r.name }),
                );
              } catch {}

              // remove any legacy inline banner injected previously
              try {
                const container = document.querySelector(".container");
                if (container) {
                  const children = Array.from(container.children);
                  for (const ch of children) {
                    const txt = (ch.textContent || "").trim();
                    if (txt.startsWith("Restaurant:") || txt.startsWith("Restaurant: ")) {
                      ch.remove();
                    }
                  }
                }
              } catch {}

              // Update header brand title so it shows restaurant name
              try {
                const headerTitle = document.querySelector("header .brand h1");
                if (headerTitle) headerTitle.textContent = r.name;
              } catch {}
            }
          })
          .catch(() => {
            try {
              localStorage.removeItem("restaurant_name");
            } catch {}
          });
      }
    } catch {}
  } else {
    try {
      localStorage.removeItem("restaurant_name");
      // also restore default brand title if present
      const headerTitle = document.querySelector("header .brand h1");
      if (headerTitle) headerTitle.textContent = "Sabor Original";
    } catch {}
  }
});

onUnmounted(() => {
  if (checkInterval) window.clearInterval(checkInterval);
  try {
    window.removeEventListener("storage", onStorage);
  } catch {}
});

/* Computed filtered items based on selected category */
const filteredItems = computed(() => {
  if (selectedCategory.value === "Todo el menú") return menuItems.value;
  return menuItems.value.filter((it: MenuItemWithImage) => {
    const cat = it.category || "";
    const lowerCat = cat.toLowerCase();
    const sel = selectedCategory.value.toLowerCase();
    return lowerCat === sel || lowerCat.includes(sel);
  });
});
</script>

<template>
  <main>
    <HeroCarousel />
    <div class="container page">
      <div v-if="hasActiveSession" class="call-waiter-section">
        <button @click="callWaiter" :disabled="callingWaiter" class="call-waiter-btn">
          <span v-if="!callingWaiter">Llamar Mesero</span>
          <span v-else>Llamando...</span>
        </button>
        <p v-if="callSuccess" class="call-success">✓ Mesero notificado</p>
      </div>
      <section
        v-if="hasActiveSession"
        id="mi-pedido"
        class="receipt-card"
        aria-label="Resumen de pedidos"
      >
        <div class="receipt-header">
          <h2>Mi Pedido</h2>
          <span v-if="hasReceipt" class="receipt-subtotal">Subtotal: {{ formattedSubtotal }}</span>
        </div>
        <p v-if="!hasReceipt" class="receipt-empty">Aún no hay pedidos enviados.</p>
        <div v-else class="receipt-orders">
          <article v-for="order in receiptOrders" :key="order.id" class="receipt-order">
            <header>
              <div class="receipt-order-info">
                <strong>Orden #{{ order.id }}</strong>
                <span v-if="order.createdAt" class="order-time">
                  · {{ formatOrderTime(order.createdAt) }}
                </span>
              </div>
              <span :class="['order-status', getStatusClass(order.status)]">
                {{ getStatusLabel(order.status) }}
              </span>
            </header>
            <ul>
              <li v-for="(item, index) in order.items" :key="index" class="receipt-item">
                <span class="item-name">{{ item.quantity }} × {{ item.name }}</span>
                <span class="item-total">{{ formatCurrency(item.lineTotal) }}</span>
              </li>
            </ul>
            <footer>
              <span>Total del pedido (IVA incluido)</span>
              <span>{{ formatCurrency(order.total) }}</span>
            </footer>
          </article>
        </div>
      </section>
      <div v-if="loading" class="loading">Cargando...</div>

      <div v-else-if="error" class="error">
        {{ error }}
      </div>

      <div v-else-if="menuItems.length === 0" class="empty">
        No se encontraron elementos en el menú.
      </div>

      <div v-else id="menu" class="menu-section">
        <div class="categories">
          <div class="category-list">
            <button
              v-for="cat in categories"
              :key="cat"
              :class="['category-btn', { active: selectedCategory === cat }]"
              @click="selectedCategory = cat"
            >
              {{ cat }}
            </button>
          </div>
        </div>

        <transition-group name="list" tag="div" class="menu-grid">
          <div v-for="item in filteredItems" :key="item.id" class="menu-item">
            <div class="thumb-wrap">
              <img
                v-if="(item as any).image"
                :src="(item as any).image"
                :alt="item.name"
                class="thumb-img"
                loading="lazy"
              />
              <div v-else class="thumb placeholder" aria-hidden="true"></div>
            </div>
            <div class="content">
              <h3>{{ item.name }}</h3>
              <p class="desc">{{ item.description }}</p>
              <div class="meta">
                <button
                  class="status add"
                  :class="{ available: item.isAvailable, unavailable: !item.isAvailable }"
                  type="button"
                  aria-label="Disponible"
                  title="Disponible"
                >
                  Disponible
                </button>
                <span class="price">₡{{ item.price.toFixed(2) }}</span>
              </div>
            </div>
          </div>
        </transition-group>
      </div>
    </div>
  </main>
</template>

<style scoped>
.container.page {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1rem 1.25rem;
}

.call-waiter-section {
  text-align: center;
  margin-bottom: 2rem;
  padding: 1rem;
  background: var(--color-background-soft);
  border-radius: 8px;
}

.call-waiter-btn {
  background: linear-gradient(135deg, #ff9800 0%, #ff5722 100%);
  color: white;
  border: none;
  padding: 1rem 2rem;
  border-radius: 999px;
  font-size: 1.1rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
  box-shadow: 0 4px 15px rgba(255, 152, 0, 0.3);
}

.call-waiter-btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(255, 152, 0, 0.4);
}

.call-waiter-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.call-success {
  color: #4caf50;
  font-weight: 600;
  margin-top: 0.5rem;
  animation: fadeIn 0.3s ease;
}

.receipt-card {
  background: #fff7ec;
  border: 1px solid rgba(255, 152, 0, 0.25);
  border-radius: 12px;
  padding: 1.5rem;
  margin-bottom: 2rem;
  box-shadow: 0 12px 30px rgba(255, 152, 0, 0.08);
}

.receipt-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.receipt-header h2 {
  margin: 0;
  font-size: 1.4rem;
  color: #ff6f00;
}

.receipt-subtotal {
  font-weight: 700;
  color: #e65100;
}

.receipt-empty {
  text-align: center;
  color: #666;
  font-style: italic;
  margin: 0;
}

.receipt-orders {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.receipt-order {
  background: #ffffff;
  border-radius: 10px;
  padding: 1rem;
  border: 1px solid rgba(255, 152, 0, 0.15);
  box-shadow: 0 4px 16px rgba(255, 152, 0, 0.1);
}

.receipt-order header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.receipt-order-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: #333;
}

.order-time {
  color: #999;
  font-size: 0.9rem;
}

.order-status {
  font-size: 0.85rem;
  font-weight: 600;
  padding: 0.35rem 0.7rem;
  border-radius: 999px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.order-status.sent {
  background: rgba(33, 150, 243, 0.12);
  color: #1976d2;
}

.order-status.ready {
  background: rgba(76, 175, 80, 0.15);
  color: #2e7d32;
}

.order-status.pending {
  background: rgba(158, 158, 158, 0.2);
  color: #424242;
}

.receipt-order ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.receipt-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.95rem;
  color: #333;
}

.receipt-item .item-name {
  font-weight: 500;
}

.receipt-item .item-total {
  font-weight: 600;
}

.receipt-order footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid rgba(0, 0, 0, 0.08);
  font-weight: 600;
  color: #2e7d32;
}

@media (max-width: 640px) {
  .receipt-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .receipt-order header {
    flex-direction: column;
    align-items: flex-start;
  }

  .receipt-order footer {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.hero {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem 0 1.5rem;
}

.hero h2 {
  margin: 0 0 0.25rem;
  font-size: 1.6rem;
}
.lead {
  color: white;
  opacity: 0.9;
}

.categories {
  width: 100%;
  margin-top: 0.75rem;
  margin-bottom: 1.25rem;
}
.category-list {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.category-btn {
  border: 1px solid var(--color-accent);
  background: transparent;
  padding: 0.4rem 0.75rem;
  border-radius: 999px;
  cursor: pointer;
  font-weight: 600;
  color: var(--color-heading);
}
.category-btn.active {
  background: var(--color-accent);
  color: white;
}
.category-btn {
  transition:
    background-color 200ms ease,
    transform 150ms ease,
    border-color 200ms ease;
}
.category-btn:hover {
  border-color: var(--color-accent);
  transform: translateY(-2px);
}

.loading,
.error,
.empty {
  padding: 1.5rem;
  text-align: center;
  border-radius: 8px;
  background-color: var(--color-background-soft);
}

.error {
  color: #d32f2f;
}

.menu-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 1.25rem;
  align-items: stretch;
  grid-auto-rows: 1fr;
}

.menu-item {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: 10px;
  background-color: var(--color-background-soft);
  box-shadow: 0 6px 18px rgba(20, 20, 20, 0.05);
  transition:
    transform 0.18s ease,
    box-shadow 0.18s ease;
  height: 100%;
}

.menu-item:hover {
  transform: translateY(-4px);
  box-shadow: 0 10px 30px rgba(20, 20, 20, 0.07);
}

.thumb.placeholder {
  height: 180px;
  background: linear-gradient(135deg, rgba(44, 62, 80, 0.06), rgba(44, 62, 80, 0.02));
  border-top-left-radius: 10px;
  border-top-right-radius: 10px;
}

.thumb-img {
  width: 100%;
  height: 180px;
  display: block;
  object-fit: cover;
  border-top-left-radius: 10px;
  border-top-right-radius: 10px;
}

.thumb-wrap {
  position: relative;
}

.status-badge {
  position: absolute;
  top: 10px;
  left: 10px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 0.28rem 0.45rem;
  border-radius: 999px;
  color: #fff;
  font-weight: 700;
  font-size: 0.78rem;
  line-height: 1;
  box-shadow: 0 6px 14px rgba(20, 20, 20, 0.12);
}
.status-badge.available {
  background-color: #4caf50;
}
.status-badge.unavailable {
  background-color: #f44336;
}
.status-badge svg {
  display: block;
  color: rgba(255, 255, 255, 0.95);
}

.status-text {
  margin-left: 6px;
  font-size: 0.82rem;
  font-weight: 600;
  color: inherit;
  display: inline-block;
}

.content {
  padding: 1rem;
  display: flex;
  flex-direction: column;
  flex: 1 1 auto;
}

.content h3 {
  margin: 0 0 0.5rem;
}
.desc {
  color: white;
  opacity: 0.9;
  margin-bottom: 0.75rem;
}

.meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-top: auto;
}
.category {
  font-size: 0.9rem;
  color: var(--color-text);
  opacity: 0.8;
}
.price {
  font-weight: 700;
  color: var(--color-accent);
}

/* Transition styles for list items */
.list-enter-active,
.list-leave-active {
  transition: all 240ms ease;
}
.list-enter-from {
  opacity: 0;
  transform: translateY(8px);
}
.list-enter-to {
  opacity: 1;
  transform: translateY(0);
}
.list-leave-from {
  opacity: 1;
  transform: translateY(0);
}
.list-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

.status {
  font-size: 0.85rem;
  font-weight: 600;
  padding: 0.35rem 0.6rem;
  border-radius: 6px;
  display: inline-block;
  border: none;
  background-clip: padding-box;
}
.status.available {
  background-color: #4caf50;
  color: #fff;
}
.status.unavailable {
  background-color: #f44336;
  color: #fff;
}

.status:focus {
  outline: none;
  box-shadow: none;
}

/* Responsive styles */
@media (max-width: 540px) {
  .hero {
    flex-direction: column;
    align-items: flex-start;
  }
  .thumb.placeholder {
    height: 140px;
  }
  .thumb-img {
    height: 140px;
  }
  .menu-grid {
    grid-template-columns: 1fr;
  }
}
</style>
