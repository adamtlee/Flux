import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './services/auth.service';
import { SidebarComponent } from './components/sidebar/sidebar.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, CommonModule, SidebarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'Flux';
  authService = inject(AuthService);
  private router = inject(Router);

  isAuthenticated$ = this.authService.isAuthenticated$;
  currentUser$ = this.authService.currentUser$;
  isSidebarOpen = false;
  isSidebarCollapsed = false;

  toggleSidebar(): void {
    if (this.isMobileViewport()) {
      this.isSidebarOpen = !this.isSidebarOpen;
      return;
    }

    this.isSidebarCollapsed = !this.isSidebarCollapsed;
  }

  closeSidebar(): void {
    if (this.isMobileViewport()) {
      this.isSidebarOpen = false;
    }
  }

  onSidebarToggleRequested(): void {
    this.toggleSidebar();
  }

  onSidebarItemSelected(): void {
    this.closeSidebar();
  }

  logout(): void {
    this.authService.logout();
    this.closeSidebar();
    this.router.navigate(['/']);
  }

  private isMobileViewport(): boolean {
    return typeof window !== 'undefined' && window.matchMedia('(max-width: 1024px)').matches;
  }
}
