import { api } from './api'

export interface CreateWaiterRequest {
  restaurantId: number
  name: string
  password?: string
}

export interface LoginRequest {
  restaurantId: number
  name: string
  password?: string
}

export async function createWaiter(req: CreateWaiterRequest) {
  const resp = await api.post('/waiters', req)
  return resp.data
}

export async function loginWaiter(req: LoginRequest) {
  const resp = await api.post('/waiters/login', req)
  return resp.data
}

export async function getWaitersByRestaurant(restaurantId: number) {
  const resp = await api.get(`/waiters/byrestaurant/${restaurantId}`)
  return resp.data
}
