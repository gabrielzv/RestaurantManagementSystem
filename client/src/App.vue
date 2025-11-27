<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { RouterView, useRoute, type RouteMeta } from "vue-router";

const navOpen = ref(false);
const route = useRoute();

// Use route meta to hide header/footer when needed (not in the login or code access view)
const hideLayout = computed(() => {
  try {
    return !!(route.meta as RouteMeta)?.hideLayout;
  } catch {
    return false;
  }
});
const showHeader = computed(() => !hideLayout.value);
const showFooter = computed(() => !hideLayout.value);

// Restaurant name shown in header/footer. Read from localStorage if available. Sabor Original is default.
const restaurantName = ref<string>("Sabor Original");

const updateRestaurantNameFromStorage = () => {
  try {
    const name = localStorage.getItem("restaurant_name");
    if (name && name.trim().length > 0) restaurantName.value = name;
    else restaurantName.value = "Sabor Original";
  } catch {
    restaurantName.value = "Sabor Original";
  }
};

// On component mount, read restaurant name from localStorage and set up event listeners
onMounted(() => {
  updateRestaurantNameFromStorage();

  // Listen for custom event dispatched by HomeView when name is set in the same window
  const onNameChanged = (e: Event) => {
    try {
      const custom = e as CustomEvent<string>;
      if (custom && custom.detail) restaurantName.value = custom.detail;
      else updateRestaurantNameFromStorage();
    } catch {
      updateRestaurantNameFromStorage();
    }
  };

  window.addEventListener("restaurant-name-changed", onNameChanged as EventListener);

  // Also listen for storage events so other tabs/windows update
  const onStorage = (e: StorageEvent) => {
    if (e.key === "restaurant_name") updateRestaurantNameFromStorage();
  };
  window.addEventListener("storage", onStorage);

  // cleanup
  onBeforeUnmount(() => {
    window.removeEventListener("restaurant-name-changed", onNameChanged as EventListener);
    window.removeEventListener("storage", onStorage);
  });
});
</script>

<template>
  <header class="site-header" v-if="showHeader">
    <div class="container">
      <div class="brand">
        <h1>{{ restaurantName }}</h1>
        <p class="tag">
          La esencia de una buena comida en cada plato. Sabores tradicionales y extranjeros que te
          harán sentir en casa.
        </p>
      </div>

      <button
        class="burger"
        @click="navOpen = !navOpen"
        aria-label="Toggle menu"
        :aria-expanded="navOpen"
      >
        <span></span>
        <span></span>
        <span></span>
      </button>
    </div>
  </header>

  <main>
    <RouterView />
  </main>

  <footer class="site-footer" v-if="showFooter">
    <div class="container footer-inner">
      <div>
        <strong>{{ restaurantName }}</strong>
        <div class="small muted">© 2025 EGR & GZV. Todos los derechos reservados.</div>
      </div>
    </div>
  </footer>
</template>

<style scoped>
.site-header {
  background: var(--color-footer-header-bg);
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  color: var(--color-footer-text);
  position: relative;
  z-index: 40;
}

/* Header and footer backgrounds with full width */
.site-header,
.site-footer {
  width: 100vw;
  position: relative;
  left: 50%;
  transform: translateX(-50%);
  box-sizing: border-box;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 1rem 1.25rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.brand h1 {
  margin: 0;
  font-size: 1.25rem;
  color: var(--color-accent);
  font-family: var(--font-heading);
  letter-spacing: 0.2px;
}

.brand .tag {
  margin-top: 0.15rem;
  font-size: 1rem;
  color: var(--color-footer-text);
}

.site-nav {
  display: flex;
  gap: 0.75rem;
}

/* Burger menu lines */
.burger {
  display: none;
  background: transparent;
  border: none;
  gap: 4px;
  padding: 6px;
  cursor: pointer;
}

.burger span {
  display: block;
  width: 22px;
  height: 2px;
  background: var(--color-heading);
  margin: 4px 0;
  border-radius: 2px;
}

.site-nav a {
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  text-decoration: none;
  background: transparent;
  color: rgba(255, 255, 255, 0.95);
  border-color: var(--color-accent);
  border-radius: 999px;
  border: 1px solid var(--color-accent);
}

.site-nav a.router-link-exact-active {
  background-color: var(--color-accent);
}

.site-nav a:hover {
  background-color: var(--color-accent);
  color: white;
}

main {
  min-height: calc(100vh - 160px);
  padding: 0 0 1.5rem;
}

.site-footer {
  border-top: 1px solid rgba(255, 255, 255, 0.04);
  background: var(--color-footer-header-bg);
  color: var(--color-footer-text);
}

.footer-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1.25rem;
}

.footer-nav {
  display: flex;
  gap: 0.75rem;
}

.muted {
  font-size: 0.85rem;
  color: var(--color-accent);
  opacity: 0.85;
}

/* Responsive styles */
@media (max-width: 720px) {
  .container {
    flex-direction: column;
    align-items: flex-start;
  }

  .site-nav {
    width: 100%;
    justify-content: flex-start;
    display: none;
    flex-direction: column;
    gap: 0.5rem;
    margin-top: 0.5rem;
    width: 100%;
  }

  .site-nav.open {
    display: flex;
  }

  .burger {
    display: block;
  }
  .footer-inner {
    flex-direction: column;
    gap: 0.5rem;
    align-items: flex-start;
  }
}
</style>
