<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { validateAccessCode } from '@/services/accessCodeService'

const code = ref('')
const error = ref('')
const loading = ref(false)
const router = useRouter()

const submit = async () => {
  error.value = ''
  if (!code.value || code.value.length !== 4) {
    error.value = 'Ingrese un código de 4 dígitos'
    return
  }

  loading.value = true
  try {
    const data = await validateAccessCode(code.value)
    // store session info locally
    localStorage.setItem('access_session', JSON.stringify(data))
    // navigate to menu
    router.push({ name: 'menu' })
  } catch (err: any) {
    if (err.response && err.response.status === 404) {
      error.value = 'Código no encontrado'
    } else if (err.response && err.response.status === 400) {
      error.value = err.response.data || 'Código inválido o expirado'
    } else {
      error.value = 'Error al validar el código. Intente de nuevo.'
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
      <h1>Ingrese el código para ver el menú</h1>

      <input v-model="code" maxlength="4" placeholder="0000" />

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
  font-size: 2rem;
  text-align: center;
  width: 8rem;
  padding: 0.5rem;
  margin: 1rem 0;
}

.actions button {
  padding: 0.75rem 1.5rem;
  font-size: 1rem;
}

.error {
  color: #d32f2f;
  margin-top: 1rem;
}
</style>
