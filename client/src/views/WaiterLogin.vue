<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { loginWaiter } from "@/services/waitersService";

const username = ref("");
const password = ref("");
const error = ref("");
const loading = ref(false);
const router = useRouter();

const submit = async () => {
  error.value = "";
  if (!username.value || !password.value) {
    error.value = "Usuario y Contraseña son requeridos";
    return;
  }

  loading.value = true;
  try {
    const data = await loginWaiter({
      username: username.value,
      password: password.value,
    });
    // store waiter token for authentication
    const token = data.token || data.Token;
    localStorage.setItem("waiter_token", token);
    // store waiter session data
    localStorage.setItem(
      "waiter_session",
      JSON.stringify({
        token: token,
        waiterId: data.id || data.Id,
        restaurantId: data.restaurantId || data.RestaurantId,
        username: data.username || data.Username,
        expiresAt: data.expiresAt || data.ExpiresAt,
      }),
    );
    router.push({ name: "waiter-panel" });
  } catch (err: unknown) {
    const axiosError = err as { response?: { status?: number } };
    if (axiosError.response && axiosError.response.status === 401) {
      error.value = "Credenciales inválidas";
    } else {
      error.value = "Error al iniciar sesión";
      console.error(err);
    }
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <main class="center">
    <div class="login-card">
      <h1>Ingreso Mesero</h1>

      <div class="input-group">
        <input v-model="username" placeholder="Usuario" />
      </div>        <div class="input-group">
          <input v-model="password" placeholder="Contraseña" type="password" />
        </div>

        <div class="actions">
          <button @click="submit" :disabled="loading">
            {{ loading ? "Ingresando..." : "Entrar" }}
          </button>
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

.login-card {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 400px;
}

h1 {
  text-align: center;
  margin-bottom: 1.5rem;
  color: #333;
}

.input-group {
  margin-bottom: 1rem;
}

input {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
  box-sizing: border-box;
}

input:focus {
  outline: none;
  border-color: #007bff;
}

.actions {
  margin-top: 1.5rem;
  text-align: center;
}

button {
  background: #007bff;
  color: white;
  border: none;
  padding: 0.75rem 2rem;
  border-radius: 4px;
  font-size: 1rem;
  cursor: pointer;
  width: 100%;
}

button:hover:not(:disabled) {
  background: #0056b3;
}

button:disabled {
  background: #ccc;
  cursor: not-allowed;
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
