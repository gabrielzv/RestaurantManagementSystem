<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import { createAccessCode, getAccessCodesByWaiter } from "@/services/accessCodeService";

const sessionRaw = localStorage.getItem("waiter_session");
const session = sessionRaw ? JSON.parse(sessionRaw) : null;
const tableNumber = ref("");
const ttl = ref<number | null>(60);
const generated = ref<{ code?: string; expiresAt?: string } | null>(null);
const error = ref("");
const loading = ref(false);
const router = useRouter();
const codes = ref<
  Array<{ id: number; code: string; tableNumber?: string; usedAt?: string; waiterId?: number }>
>([]);

import { notificationService } from "@/services/notificationService";
import { orderSession } from "@/services/orderSession";
const statuses = ref<Record<string, string>>({});

// Refresh statuses for all codes
const refreshStatuses = () => {
  try {
    for (const c of codes.value) {
      const tbl = c.tableNumber;
      if (!tbl) continue;
      const s = orderSession.load(session?.waiterId, tbl);
      if (s && s.status) {
        statuses.value[tbl] = s.status;
        continue;
      }
      const nots = notificationService.get(session?.waiterId, tbl);
      if (Array.isArray(nots) && nots.length > 0) {
        const first = nots[0];
        if (first && first.message) {
          statuses.value[tbl] = first.message;
        }
      }
    }
  } catch (err) {
    console.warn("refreshStatuses failed", err);
  }
};

if (!session) {
  router.push({ name: "waiter-login" });
}

const loadCodes = async () => {
  if (!session) return;
  try {
    const data = await getAccessCodesByWaiter(session.waiterId);
    codes.value = data;
    try {
      refreshStatuses();
    } catch {}
  } catch (err) {
    console.error(err);
  }
};

const generate = async () => {
  error.value = "";
  if (!session) return;

  // Validate table number is required
  if (!tableNumber.value || tableNumber.value.trim() === "") {
    error.value = "El número de mesa es obligatorio";
    return;
  }

  loading.value = true;
  try {
    const req = {
      restaurantId: session.restaurantId,
      tableNumber: tableNumber.value,
      waiterId: session.waiterId || undefined,
      ttlMinutes: ttl.value || undefined,
    };
    const data = await createAccessCode(req);
    generated.value = data;
    await loadCodes(); // reload after generate
  } catch (err: unknown) {
    console.error(err);
    error.value = "Error al generar código";
  } finally {
    loading.value = false;
  }
};

const logout = () => {
  localStorage.removeItem("waiter_session");
  router.push({ name: "waiter-login" });
};

const attendTable = (tableNumber: string, waiterId: number, code: string) => {
  router.push({
    name: "waiter-table",
    params: { tableNumber, waiterId: waiterId.toString(), code },
  });
};

// Load codes on mount
onMounted(() => {
  loadCodes();
  setInterval(loadCodes, 5000);
  // initialize statuses per code
  refreshStatuses();
  const unsub = notificationService.subscribe(
    ({ waiterId: wid, tableNumber: tbl, notification }) => {
      if (!session) return;
      if (wid === session.waiterId && tbl) {
        statuses.value[tbl] = notification.message;
      }
    },
  );
  onUnmounted(() => {
    try {
      unsub();
    } catch {}
  });
});

// Also load codes when component is activated (when returning from other pages)
import { onActivated } from "vue";
onActivated(() => {
  loadCodes();
});
</script>

<template>
  <main class="center">
    <div class="panel-card">
      <h1>Panel Mesero</h1>
      <p class="welcome">Bienvenido {{ session?.username || "Mesero" }}</p>
      <div class="form-section">
        <div class="form-row">
          <input v-model="tableNumber" placeholder="Número de mesa *" required />
        </div>
        <div class="actions">
          <button @click="generate" :disabled="loading">
            {{ loading ? "Generando..." : "Generar código" }}
          </button>
          <button @click="logout" class="secondary">Salir</button>
        </div>
      </div>
      <div v-if="generated" class="generated">
        <h3>¡Código generado!</h3>
        <strong>{{ generated.code }}</strong>
        <p>Expira: {{ generated.expiresAt || "Nunca" }}</p>
      </div>

      <div class="codes">
        <h3>Códigos Generados</h3>
        <transition-group name="code-item" tag="ul" v-if="codes.length > 0">
          <li v-for="code in codes" :key="code.id" class="code-item">
            <div class="code-info">
              <div class="code-number">{{ code.code }}</div>
              <div class="code-details">
                <div>Mesa: {{ code.tableNumber || "N/A" }}</div>
                <transition name="status" mode="out-in">
                  <div
                    v-if="code.tableNumber && statuses[code.tableNumber]"
                    :key="`status-${code.tableNumber}`"
                    class="name status-badge"
                  >
                    Estado: {{ statuses[code.tableNumber] }}
                  </div>
                </transition>
              </div>
            </div>
            <div class="code-status" :class="code.usedAt ? 'used' : 'generated'">
              {{ code.usedAt ? "Usado" : "Generado" }}
            </div>
            <button
              v-if="code.usedAt && code.tableNumber"
              @click="attendTable(code.tableNumber, session.waiterId, code.code)"
              class="attend-btn"
            >
              Atender Mesa
            </button>
          </li>
        </transition-group>
        <p v-else style="text-align: center; color: #666; margin: 2rem 0">
          No hay códigos generados aún
        </p>
      </div>

      <p class="error" v-if="error">{{ error }}</p>
    </div>
  </main>
</template>

<style scoped>
.notifications {
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 1.5rem;
  color: #000000;
}

.name {
  color: #000000;
}

.center {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  padding: 2rem;
}

.panel-card {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 600px;
}

h1 {
  text-align: center;
  margin-bottom: 1rem;
  color: #333;
}

.welcome {
  text-align: center;
  color: #666;
  margin-bottom: 2rem;
}

.form-section {
  background: #f8f9fa;
  padding: 1.5rem;
  border-radius: 6px;
  margin-bottom: 2rem;
}

.form-row {
  display: flex;
  gap: 1rem;
  margin-bottom: 1rem;
  align-items: center;
}

input {
  flex: 1;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-top: 1rem;
}

button {
  background: #007bff;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
}

button:hover:not(:disabled) {
  background: #0056b3;
}

button:disabled {
  background: #ccc;
  cursor: not-allowed;
}

button.secondary {
  background: #6c757d;
}

.generated {
  background: #d4edda;
  border: 1px solid #c3e6cb;
  color: #155724;
  padding: 1rem;
  border-radius: 6px;
  margin-top: 1rem;
  text-align: center;
}

.generated strong {
  font-size: 1.5rem;
  font-family: monospace;
  display: block;
  margin: 0.5rem 0;
}

.codes {
  background: #f8f9fa;
  padding: 1.5rem;
  border-radius: 6px;
}

.codes h3 {
  margin-bottom: 1rem;
  text-align: center;
  color: #333;
}

.codes ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.code-item {
  background: white;
  border: 1px solid #ddd;
  border-radius: 6px;
  padding: 1rem;
  margin-bottom: 0.5rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.code-info {
  flex: 1;
}

.code-number {
  font-size: 1.2rem;
  font-weight: bold;
  font-family: monospace;
  color: #000;
  margin-bottom: 0.25rem;
}

.code-details {
  color: #666;
  font-size: 0.9rem;
}

.code-status {
  padding: 0.25rem 0.5rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
}

.code-status.used {
  background: #d4edda;
  color: #155724;
}

.code-status.generated {
  background: #fff3cd;
  color: #856404;
}

.attend-btn {
  background: #28a745;
  padding: 0.4rem 0.8rem;
  font-size: 0.9rem;
}

/* Responsive styles */
@media (max-width: 600px) {
  .panel-card {
    padding: 1rem;
  }
  .form-row {
    flex-direction: column;
    align-items: stretch;
  }
  .actions {
    flex-direction: column;
    gap: 0.5rem;
    margin-top: 0.75rem;
  }
  .actions button {
    width: 100%;
    padding: 0.6rem 0.75rem;
  }
  .codes {
    padding: 1rem;
  }
  .code-item {
    flex-direction: column;
    align-items: stretch;
  }
  .code-status {
    margin-top: 0.5rem;
    align-self: flex-start;
  }
  .attend-btn {
    width: 100%;
    margin-top: 0.5rem;
  }
  .generated {
    padding: 0.75rem;
  }
  .code-number {
    font-size: 1rem;
  }
}

@media (max-width: 400px) {
  .panel-card {
    padding: 0.75rem;
  }
  .welcome {
    font-size: 0.95rem;
  }
  .code-number {
    font-size: 0.95rem;
  }
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

/* Transition for status badge */
.status-enter-from,
.status-leave-to {
  opacity: 0;
  transform: translateY(-6px) scale(0.98);
}
.status-enter-active,
.status-leave-active {
  transition: all 220ms cubic-bezier(0.2, 0.9, 0.2, 1);
}
.status-enter-to,
.status-leave-from {
  opacity: 1;
  transform: translateY(0) scale(1);
}

/* Transition-group for code items */
.code-item-enter-from {
  opacity: 0;
  transform: translateY(-8px) scale(0.995);
}
.code-item-enter-active {
  transition: all 260ms cubic-bezier(0.2, 0.9, 0.2, 1);
}
.code-item-leave-to {
  opacity: 0;
  transform: translateY(6px);
}
.code-item-leave-active {
  transition: all 200ms ease;
}
</style>
