import { api } from './api'

export async function getRestaurantById(id: number) {
  const resp = await api.get(`/restaurants/${id}`)
  return resp.data
}
