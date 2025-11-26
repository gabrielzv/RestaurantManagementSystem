import { createRouter, createWebHistory } from "vue-router";
import HomeView from "../views/HomeView.vue";
import CodeEntry from "../views/CodeEntry.vue";
import WaiterLogin from "../views/WaiterLogin.vue";
import WaiterPanel from "../views/WaiterPanel.vue";
import WaiterTable from "../views/WaiterTable.vue";

// Helper function to check if waiter is authenticated
function isWaiterAuthenticated(): boolean {
  const token = localStorage.getItem("waiter_token");
  return !!token;
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "code",
      component: CodeEntry,
      meta: { hideLayout: true },
    },
    {
      path: "/menu",
      name: "menu",
      component: HomeView,
    },
    {
      path: "/waiter/login",
      name: "waiter-login",
      component: WaiterLogin,
      meta: { hideLayout: true },
    },
    {
      path: "/waiter/panel",
      name: "waiter-panel",
      component: WaiterPanel,
      meta: { requiresAuth: true, hideLayout: true },
    },
    {
      path: "/waiter/table/:tableNumber/:waiterId/:code",
      name: "waiter-table",
      component: WaiterTable,
      meta: { requiresAuth: true, hideLayout: true },
    },
    {
      path: "/waiter/:pathMatch(.*)*",
      name: "waiter-catch-all",
      redirect: { name: "waiter-login" },
      meta: { requiresAuth: true },
    },
    {
      path: "/about",
      name: "about",
      component: () => import("../views/AboutView.vue"),
    },
  ],
});

// Navigation guard to protect waiter routes
router.beforeEach((to, from, next) => {
  const isAuth = isWaiterAuthenticated();

  // Protect all /waiter/* routes except /waiter/login
  const isWaiterRoute = to.path.startsWith("/waiter/") && to.path !== "/waiter/login";

  if ((to.meta.requiresAuth || isWaiterRoute) && !isAuth) {
    // Redirect to waiter login if not authenticated
    next({ name: "waiter-login" });
    return;
  }

  // Additional validation for waiter-table route
  if (to.name === "waiter-table" && isAuth) {
    try {
      const session = localStorage.getItem("waiter_session");
      if (session) {
        const sessionData = JSON.parse(session);
        const sessionWaiterId = sessionData.waiterId;
        const routeWaiterId = to.params.waiterId;

        // Check if the waiterId in the URL matches the logged-in waiter
        if (sessionWaiterId && routeWaiterId && String(sessionWaiterId) !== String(routeWaiterId)) {
          // Unauthorized access attempt - redirect to panel
          alert("No tienes permiso para acceder a esta mesa.");
          next({ name: "waiter-panel" });
          return;
        }
      }
    } catch (e) {
      console.error("Error validating waiter access:", e);
    }
  }

  next();
});

export default router;
