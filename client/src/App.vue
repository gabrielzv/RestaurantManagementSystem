<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header>
    <div class="wrapper">
      <h1>Restaurant Management System</h1>
      <nav>
        <RouterLink to="/">Menu Items</RouterLink>
        <RouterLink to="/about">About</RouterLink>
      </nav>
    </div>
  </header>

  <RouterView />
</template>

<style scoped>
header {
  line-height: 1.5;
  background-color: var(--color-background-soft);
  padding: 1rem;
}

.wrapper {
  max-width: 1200px;
  margin: 0 auto;
}

h1 {
  margin: 0 0 1rem;
}

nav {
  display: flex;
  gap: 1rem;
}

nav a {
  padding: 0.5rem 1rem;
  border-radius: 4px;
  text-decoration: none;
  transition: background-color 0.3s;
}

nav a.router-link-exact-active {
  background-color: var(--color-background-mute);
}

nav a:hover {
  background-color: var(--color-background-mute);
}
</style>
