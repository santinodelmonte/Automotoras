// Los datos que comparten el servidor de los tests y los tests.
//
// La contraseña no es un secreto: es la de una base SQLite que se crea y se borra en cada
// corrida, en la máquina de quien corre los tests. Los usuarios son los del seed de
// desarrollo (SeedDeDesarrollo.cs).

export const PUERTO = 5199
export const BASE = `http://localhost:${PUERTO}`
export const PASSWORD_DEL_SEED = 'E2e-solo-pruebas-1'

export const USUARIOS = {
  superAdmin: 'super@automotoras.uy',
  ownerNorte: 'owner@norte.uy',
  vendedorNorte: 'vendedor@norte.uy',
}
