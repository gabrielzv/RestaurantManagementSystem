<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { menuItemService, type MenuItem } from '@/services/api'
import { getRestaurantById } from '@/services/restaurantsService'

const menuItems = ref<MenuItem[]>([])
const loading = ref(true)
const error = ref('')

const loadMenuItems = async () => {
  try {
    loading.value = true
    error.value = ''
    menuItems.value = await menuItemService.getAll()
  } catch (err) {
    error.value = 'Failed to load menu items. Make sure the API is running.'
    console.error('Error loading menu items:', err)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadMenuItems()
  // load session restaurant info if available
  const session = localStorage.getItem('access_session')
  if (session) {
    try {
      const s = JSON.parse(session)
      if (s && s.restaurantId) {
        getRestaurantById(s.restaurantId).then(r => {
          if (r && r.name) {
            // show a simple welcome message
            const banner = document.createElement('div')
            banner.style.padding = '1rem'
            banner.style.backgroundColor = '#f5f5f5'
            banner.style.borderRadius = '6px'
            banner.style.marginBottom = '1rem'
            banner.innerText = `Restaurant: ${r.name}`
            const container = document.querySelector('.container')
            container && container.prepend(banner)
          }
        }).catch(() => {})
      }
    } catch {}
  }
})
</script>

<template>
  <main>
    <div class="container">
      <h2>Menu Items</h2>
      
      <div v-if="loading" class="loading">Loading...</div>
      
      <div v-else-if="error" class="error">
        {{ error }}
      </div>
      
      <div v-else-if="menuItems.length === 0" class="empty">
        No menu items found. Start by adding some items via the API.
      </div>
      
      <div v-else class="menu-grid">
        <div v-for="item in menuItems" :key="item.id" class="menu-item">
          <h3>{{ item.name }}</h3>
          <p>{{ item.description }}</p>
          <p class="category">Category: {{ item.category }}</p>
          <p class="price">${{ item.price.toFixed(2) }}</p>
          <p class="status" :class="{ available: item.isAvailable, unavailable: !item.isAvailable }">
            {{ item.isAvailable ? 'Available' : 'Unavailable' }}
          </p>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem;
}

h2 {
  margin-bottom: 2rem;
}

.loading,
.error,
.empty {
  padding: 2rem;
  text-align: center;
  border-radius: 8px;
  background-color: var(--color-background-soft);
}

.error {
  color: #d32f2f;
}

.menu-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

.menu-item {
  padding: 1.5rem;
  border-radius: 8px;
  background-color: var(--color-background-soft);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.menu-item h3 {
  margin: 0 0 0.5rem;
}

.menu-item p {
  margin: 0.5rem 0;
}

.category {
  font-size: 0.9rem;
  color: var(--color-text-mute);
}

.price {
  font-size: 1.25rem;
  font-weight: bold;
  color: var(--color-heading);
}

.status {
  font-size: 0.9rem;
  font-weight: 500;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  display: inline-block;
}

.status.available {
  background-color: #4caf50;
  color: white;
}

.status.unavailable {
  background-color: #f44336;
  color: white;
}
</style>
