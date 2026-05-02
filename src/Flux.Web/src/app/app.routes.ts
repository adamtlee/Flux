import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { BankAccountsComponent } from './pages/bank-accounts/bank-accounts.component';
import { BankAccountDetailComponent } from './pages/bank-account-detail/bank-account-detail.component';
import { LoginComponent } from './pages/login/login.component';
import { SubscriptionsComponent } from './pages/subscriptions/subscriptions.component';
import { EarningsComponent } from './pages/earnings/earnings.component';
import { AuthGuard } from './services/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: '',
    component: HomeComponent
  },
  {
    path: 'accounts',
    component: BankAccountsComponent,
    canActivate: [AuthGuard]
  },
  {
    path: 'accounts/:id',
    component: BankAccountDetailComponent,
    canActivate: [AuthGuard]
  },
  {
    path: 'subscriptions',
    component: SubscriptionsComponent,
    canActivate: [AuthGuard]
  },
  {
    path: 'earnings',
    component: EarningsComponent,
    canActivate: [AuthGuard]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
