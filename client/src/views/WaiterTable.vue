<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { clearAccessCode } from "@/services/accessCodeService";

const route = useRoute();
const router = useRouter();

const tableNumber = ref(route.params.tableNumber as string);
const waiterId = ref(route.params.waiterId as string);
const accessCode = ref(route.params.code as string);
const loading = ref(false);
const error = ref("");

// Placeholder for future notifications
const notifications = ref<string[]>([]);

// Simulate adding a notification (for demo)
const addNotification = (msg: string) => {
  notifications.value.push(msg);
};

// Example: add a test notification
setTimeout(() => addNotification("Cliente pidió agua"), 2000);

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
    // Don't auto-redirect, let user see the success message and click back
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
          <ul>
            <li
              v-for="(notif, index) in notifications"
              :key="index"
              :class="{ 'success-notification': notif.includes('desocupada') }"
            >
              {{ notif }}
            </li>
          </ul>
          <p v-if="notifications.length === 0">No hay notificaciones</p>
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
