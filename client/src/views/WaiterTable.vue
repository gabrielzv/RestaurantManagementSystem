<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";

const route = useRoute();
const router = useRouter();

const tableNumber = ref(route.params.tableNumber as string);
const waiterId = ref(route.params.waiterId as string);

// Placeholder for future notifications
const notifications = ref<string[]>([]);

// Simulate adding a notification (for demo)
const addNotification = (msg: string) => {
  notifications.value.push(msg);
};

// Example: add a test notification
setTimeout(() => addNotification("Cliente pidió agua"), 2000);

const backToPanel = () => {
  router.push({ name: "waiter-panel" });
};
</script>

<template>
  <main>
    <div class="center">
      <h1>Atendiendo Mesa {{ tableNumber }}</h1>
      <p>Mesero ID: {{ waiterId }}</p>

      <div class="notifications">
        <h3>Notificaciones</h3>
        <ul>
          <li v-for="(notif, index) in notifications" :key="index">{{ notif }}</li>
        </ul>
        <p v-if="notifications.length === 0">No hay notificaciones</p>
      </div>

      <div class="actions">
        <button @click="backToPanel">Volver al Panel</button>
      </div>
    </div>
  </main>
</template>

<style scoped>
.center {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 80vh;
}

.notifications {
  margin-top: 2rem;
  background: #f9f9f9;
  padding: 1rem;
  border-radius: 4px;
  width: 80%;
  max-width: 400px;
}

.notifications ul {
  list-style: none;
  padding: 0;
}

.notifications li {
  background: white;
  margin: 0.5rem 0;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.actions button {
  padding: 0.5rem 1rem;
  font-size: 1rem;
  margin-top: 1rem;
}
</style>
