<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { createAccessCode } from '@/services/accessCodeService'

const sessionRaw = localStorage.getItem('waiter_session')
const session = sessionRaw ? JSON.parse(sessionRaw) : null
const tableNumber = ref('')
const ttl = ref<number | null>(60)
const generated = ref<any>(null)
const error = ref('')
const loading = ref(false)
const router = useRouter()

if (!session) {
  router.push({ name: 'waiter-login' })
}

const generate = async () => {
  error.value = ''
  if (!session) return
  loading.value = true
  try {
    const req = {
      restaurantId: session.restaurantId,
      tableNumber: tableNumber.value || undefined,
      waiterId: session.waiterId || undefined,
      ttlMinutes: ttl.value || undefined
    }
    const data = await createAccessCode(req)
    generated.value = data
  } catch (err: any) {
    console.error(err)
    error.value = 'Error al generar código'
  } finally {
    loading.value = false
  }
}

const logout = () => {
  localStorage.removeItem('waiter_session')
  router.push({ name: 'waiter-login' })
}
</script>

<template>
  <main>
    <div class="center">
      <h1>Panel Mesero</h1>
      <p>Bienvenido {{ session?.name || 'Mesero' }}</p>

      <input v-model="tableNumber" placeholder="Número de mesa (opcional)" />
      <input v-model.number="ttl" placeholder="Minutos de validez" type="number" />

      <div class="actions">
        <button @click="generate" :disabled="loading">Generar código</button>
        <button @click="logout">Salir</button>
      </div>

      <div v-if="generated" class="generated">
        <h3>Código generado</h3>
        <p><strong>{{ generated.code }}</strong></p>
        <p>Expira: {{ generated.expiresAt }}</p>
      </div>

      <p class="error" v-if="error">{{ error }}</p>
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

input {
  font-size: 1rem;
  padding: 0.5rem;
  margin: 0.5rem 0;
  width: 16rem;
}

.actions button {
  padding: 0.5rem 1rem;
  font-size: 1rem;
  margin-right: 0.5rem;
}

.generated {
  margin-top: 1rem;
  background: #f5f5f5;
  padding: 1rem;
  border-radius: 4px;
}

.error {
  color: #d32f2f;
  margin-top: 1rem;
}
</style>
