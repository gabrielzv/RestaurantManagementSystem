<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import {
  clearAccessCode,
  getNotifications,
  markNotificationAsRead,
} from "@/services/accessCodeService";

const route = useRoute();
const router = useRouter();

const tableNumber = ref(route.params.tableNumber as string);
const waiterId = ref(route.params.waiterId as string);
const accessCode = ref(route.params.code as string);
const loading = ref(false);
const error = ref("");

// Notifications from API
interface Notification {
  id: number;
  tableNumber: string;
  message: string;
  createdAt: string;
}

const notifications = ref<Notification[]>([]);

const loadNotifications = async () => {
  try {
    const data = await getNotifications(accessCode.value);
    const normalized: Notification[] = (data ?? []).map((item: any) => ({
      id: item.id,
      tableNumber: item.tableNumber,
      message: item.message,
      createdAt: item.createdAt,
    }));

    for (const notif of normalized) {
      const exists = notifications.value.some((n) => n.id === notif.id);
      if (!exists) {
        notifications.value.unshift(notif);
      }
    }
  } catch (err) {
    console.error("Error loading notifications:", err);
  }
};

const dismissNotification = async (notif: Notification) => {
  try {
    await markNotificationAsRead(notif.id);
    notifications.value = notifications.value.filter((n) => n.id !== notif.id);
  } catch (err) {
    console.error("Error marking notification as read:", err);
  }
};

let notificationInterval: number | undefined;

onMounted(() => {
  loadNotifications();
  // Poll for new notifications every 3 seconds
  notificationInterval = window.setInterval(loadNotifications, 3000);
});

onUnmounted(() => {
  if (notificationInterval) window.clearInterval(notificationInterval);
});

const clearTable = async () => {
  if (!accessCode.value) {
    error.value = "No se encontró el código de acceso";
    return;
  }

  loading.value = true;
  error.value = "";
  try {
    await clearAccessCode(accessCode.value);
    // Navigate back to panel after clearing
    router.push({ name: "waiter-panel" });
  } catch (err: unknown) {
    error.value = "Error al desocupar mesa";
    console.error(err);
  } finally {
    loading.value = false;
  }
};

const backToPanel = () => {
  router.push({ name: "waiter-panel" });
};
</script>

<template>
  <main class="center">
    <div class="table-card">
      <h1>Atendiendo Mesa {{ tableNumber }}</h1>
      <p class="table-info">Mesero ID: {{ waiterId }} | Código: {{ accessCode }}</p>

      <div class="notifications">
        <h3>Notificaciones</h3>
        <ul v-if="notifications.length > 0">
          <li v-for="notif in notifications" :key="notif.id" class="notification-item">
            <div class="notification-content">
              <p>{{ notif.message }}</p>
            </div>
            <button @click="dismissNotification(notif)" class="dismiss-btn">✓</button>
          </li>
        </ul>
        <p v-else class="no-notifications">No hay notificaciones</p>
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
  max-width: 500px;
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
  animation: slideIn 0.3s ease;
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateX(-20px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

.notification-content {
  flex: 1;
}

.notification-content p {
  margin: 0;
  color: #333;
  text-align: left;
  font-style: normal;
  font-size: 1rem;
  font-weight: 500;
}

.dismiss-btn {
  background: #4caf50;
  color: white;
  border: none;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  font-size: 1.2rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  flex-shrink: 0;
  margin-left: 1rem;
}

.dismiss-btn:hover {
  background: #45a049;
  transform: scale(1.1);
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

button:hover:not(:disabled) {
  background: #c82333;
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
</style>
