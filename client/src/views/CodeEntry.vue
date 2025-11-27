<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { validateAccessCode } from "@/services/accessCodeService";

const code = ref("");
const error = ref("");
const loading = ref(false);
const router = useRouter();

const submit = async () => {
  error.value = "";
  if (!code.value || code.value.length !== 4) {
    error.value = "Ingrese un código de 4 dígitos";
    return;
  }

  loading.value = true;
  try {
    const data = await validateAccessCode(code.value);
    // store session info locally
    localStorage.setItem("access_session", JSON.stringify(data));
    // navigate to menu
    router.push({ name: "menu" });
  } catch (err: unknown) {
    const axiosError = err as { response?: { status?: number; data?: string } };
    if (axiosError.response && axiosError.response.status === 404) {
      error.value = "Código no encontrado";
    } else if (axiosError.response && axiosError.response.status === 400) {
      error.value = axiosError.response.data || "Código inválido o expirado";
    } else {
      error.value = "Error al validar el código. Intente de nuevo.";
      console.error(err);
    }
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <main class="center">
    <div class="panel-card">
      <h1>Ingrese el código para ver el menú</h1>

      <div class="input-group">
        <input v-model="code" maxlength="4" placeholder="0000" />
      </div>

      <div class="actions">
        <button @click="submit" :disabled="loading">Entrar</button>
      </div>

      <p class="error" v-if="error">{{ error }}</p>

      <div class="waiter-section">
        <p class="waiter-text">¿Usted es miembro de algún restaurante asociado?</p>
        <router-link to="/waiter/login" class="waiter-link">Inicie sesión aquí</router-link>
      </div>
    </div>
  </main>
</template>

<style scoped>
.center {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  padding: 2rem 1rem;
  box-sizing: border-box;
}

.panel-card {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  width: 100%;
  max-width: 420px;
  box-sizing: border-box;
}

.panel-card h1 {
  text-align: center;
  margin-bottom: 1.25rem;
  color: #333;
}

.input-group {
  display: flex;
  justify-content: center;
  margin-bottom: 0.75rem;
}

input {
  font-size: 2rem;
  text-align: center;
  width: 8rem;
  min-width: 120px;
  max-width: 100%;
  padding: 0.5rem;
  background-color: #fff;
  color: #111;
  border: 1px solid #ddd;
  border-radius: 6px;
  box-sizing: border-box;
  margin-bottom: 0.5rem;
}

.actions button {
  padding: 0.75rem 1.5rem;
  font-size: 1rem;
  border-radius: 6px;
  cursor: pointer;
  background: #007bff;
  color: #fff;
  border: none;
}

.actions {
  display: flex;
  gap: 1rem;
  align-items: center;
  justify-content: center;
  margin-top: 0.25rem;
}

.error {
  color: #d32f2f;
  margin-top: 1rem;
}

.waiter-section {
  margin-top: 1.5rem;
  text-align: center;
  padding-top: 0.75rem;
  border-top: 1px solid #eee;
}

.waiter-text {
  color: #000000;
  font-size: 0.95rem;
  margin-bottom: 0.75rem;
}

.waiter-link {
  display: inline-block;
  color: var(--color-accent);
  text-decoration: none;
  font-weight: 600;
  font-size: 1rem;
  padding: 0.5rem 1rem;
  border: 1px solid var(--color-accent);
  border-radius: 4px;
  transition: all 0.2s ease;
}

.waiter-link:hover {
  background-color: var(--color-accent);
  color: white;
}

/* Responsive tweaks */
@media (max-width: 720px) {
  .center h1 {
    font-size: 1.25rem;
    text-align: center;
  }

  input {
    font-size: 1.6rem;
    width: 60%;
  }

  .actions button {
    width: auto;
    padding: 0.6rem 1.25rem;
    font-size: 0.95rem;
  }

  .center {
    min-height: 50vh;
    padding: 1.5rem 1rem;
  }
}

@media (max-width: 420px) {
  input {
    width: 72%;
  }
}
</style>
