# EventosVivos — Frontend

Angular 19 SPA para el sistema de reservas de eventos en vivo.

Para instrucciones completas de instalación y arquitectura consulta el [README principal](../README.md).

---

## Requisitos

- Node.js 22+ / npm 10+

## Ejecutar en desarrollo

```bash
npm install
npm start
# Disponible en http://localhost:4200
# Conecta automáticamente al backend en http://localhost:5000
```

## Build de producción

```bash
npm run build
# Artefactos en dist/frontend/browser/
```

## Estructura

```
src/app/
├── core/
│   ├── models/         (interfaces TypeScript)
│   ├── services/       (AuthService, ApiService, LanguageService)
│   └── interceptors/   (auth JWT, language header)
├── pages/
│   ├── login/          (página de inicio de sesión con Microsoft OAuth)
│   ├── auth-callback/  (manejo del code exchange OAuth)
│   ├── events/         (listado, filtros, creación de eventos)
│   └── reservations/   (gestión de reservas del usuario)
└── shared/
    └── components/     (LanguageSwitcher)
```

## Internacionalización

La app soporta español, inglés y portugués mediante `@ngx-translate`. Los archivos de traducción están en `public/assets/i18n/`.
