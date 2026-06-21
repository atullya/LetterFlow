import client from './client';

export const login = async (data: any) => {
  const response = await client.post('/login', data);
  return response.data;
};

export const register = async (data: any) => {
  const response = await client.post('/register', data);
  return response.data;
};
