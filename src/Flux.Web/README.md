# Flux Web

This Angular application is the frontend for the Flux banking analytics system. It communicates with two backend services:
- **Auth service** (`http://localhost:5272`) — handles registration and login
- **Main API service** (`http://localhost:5271`) — provides bank account and analytics endpoints

## Development server

To start the local development server, run:

```bash
npm start
```

Or using Angular CLI directly:

```bash
ng serve
```

Once the server is running in your browser navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

### Local development setup

Make sure the backend services are running before starting the Angular dev server:

```bash
# Terminal 1: Start the Auth microservice (listens on 5272)
cd ../Flux.Auth.Api && dotnet run

# Terminal 2: Start the Main API (listens on 5271)
cd ../Flux.Api && dotnet run

# Terminal 3: Start the Angular dev server (listens on 4200, proxies to 5272 & 5271)
cd Flux.Web && npm start
```

The `proxy.conf.json` file automatically routes API requests:
- `/api/auth/*` → `http://localhost:5272`
- `/api/*` → `http://localhost:5271`

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
