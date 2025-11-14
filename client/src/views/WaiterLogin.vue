<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { loginWaiter } from '@/services/waitersService'

const restaurantId = ref<number | null>(null)
const name = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)
const router = useRouter()

const submit = async () => {
  error.value = ''
  if (!restaurantId.value || !name.value) {
    error.value = 'RestaurantId y Nombre son requeridos'
    return
  }

  loading.value = true
  try {
    const data = await loginWaiter({ restaurantId: restaurantId.value, name: name.value, password: password.value })
    // store waiter session
    localStorage.setItem('waiter_session', JSON.stringify({ token: data.token || data.Token, waiterId: data.id || data.Id, restaurantId: data.restaurantId || data.RestaurantId, name: data.name || data.Name, expiresAt: data.expiresAt || data.ExpiresAt }))
    router.push({ name: 'waiter-panel' })
  } catch (err: any) {
    if (err.response && err.response.status === 401) {
      error.value = 'Credenciales inválidas'
    } else {
      error.value = 'Error al iniciar sesión'
      console.error(err)
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main>
    <div class="center">
      <h1>Ingreso Mesero</h1>

      <input v-model.number="restaurantId" placeholder="RestaurantId" type="number" />
      <input v-model="name" placeholder="Nombre" />
      <input v-model="password" placeholder="Contraseña (opcional)" type="password" />

      <div class="actions">
        <button @click="submit" :disabled="loading">Entrar</button>
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
}

.error {
  color: #d32f2f;
  margin-top: 1rem;
}
</style>
