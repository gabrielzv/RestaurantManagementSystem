import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import CodeEntry from '../views/CodeEntry.vue'
import WaiterLogin from '../views/WaiterLogin.vue'
import WaiterPanel from '../views/WaiterPanel.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'code',
      component: CodeEntry,
    },
    {
      path: '/menu',
      name: 'menu',
      component: HomeView,
    },
    {
      path: '/waiter/login',
      name: 'waiter-login',
      component: WaiterLogin,
    },
    {
      path: '/waiter/panel',
      name: 'waiter-panel',
      component: WaiterPanel,
    },
    {
      path: '/about',
      name: 'about',
      component: () => import('../views/AboutView.vue'),
    },
  ],
})

export default router
