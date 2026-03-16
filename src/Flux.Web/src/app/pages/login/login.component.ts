import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { LoadingComponent } from '../../components/loading/loading.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  username = '';
  password = '';
  loading = false;
  error: string | null = null;
  isRegistering = false;

  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private platformId = inject(PLATFORM_ID);

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    // Redirect to accounts if already authenticated
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/accounts']);
    }
  }

  onSubmit(): void {
    if (!this.username.trim() || !this.password) {
      this.error = 'Username and password are required.';
      return;
    }

    if (this.password.length < 8 && this.isRegistering) {
      this.error = 'Password must be at least 8 characters long.';
      return;
    }

    this.loading = true;
    this.error = null;

    const request = { username: this.username, password: this.password };

    if (this.isRegistering) {
      this.authService.register(request).subscribe({
        next: () => {
          this.loading = false;
          this.navigateToAccounts();
        },
        error: (err) => {
          this.loading = false;
          this.error = err.error?.message || 'Registration failed. Please try again.';
          console.error('Registration error:', err);
        }
      });
    } else {
      this.authService.login(request).subscribe({
        next: () => {
          this.loading = false;
          this.navigateToAccounts();
        },
        error: (err) => {
          this.loading = false;
          this.error = err.error?.message || 'Login failed. Please check your credentials.';
          console.error('Login error:', err);
        }
      });
    }
  }

  toggleMode(): void {
    this.isRegistering = !this.isRegistering;
    this.error = null;
    this.password = '';
  }

  private navigateToAccounts(): void {
    const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/accounts';
    this.router.navigate([returnUrl]);
  }
}
