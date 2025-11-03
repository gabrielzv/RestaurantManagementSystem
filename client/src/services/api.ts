import axios from 'axios'

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

export const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

export interface MenuItem {
  id?: number
  name: string
  description: string
  price: number
  category: string
  isAvailable: boolean
  createdAt?: string
  updatedAt?: string
}

export const menuItemService = {
  async getAll(): Promise<MenuItem[]> {
    const response = await api.get<MenuItem[]>('/menuitems')
    return response.data
  },

  async getById(id: number): Promise<MenuItem> {
    const response = await api.get<MenuItem>(`/menuitems/${id}`)
    return response.data
  },

  async create(menuItem: MenuItem): Promise<MenuItem> {
    const response = await api.post<MenuItem>('/menuitems', menuItem)
    return response.data
  },

  async update(id: number, menuItem: MenuItem): Promise<void> {
    await api.put(`/menuitems/${id}`, menuItem)
  },

  async delete(id: number): Promise<void> {
    await api.delete(`/menuitems/${id}`)
  }
}
