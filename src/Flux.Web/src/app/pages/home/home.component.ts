import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  features = [
    {
      icon: '💼',
      title: 'Account Management',
      description: 'Easily manage your bank accounts with our intuitive interface'
    },
    {
      icon: '💰',
      title: 'Transaction Tracking',
      description: 'Monitor all your transactions in one centralized location'
    },
    {
      icon: '🔒',
      title: 'Security First',
      description: 'Your financial data is protected with enterprise-grade security'
    },
    {
      icon: '📊',
      title: 'Analytics & Insights',
      description: 'Get detailed reports and insights about your spending patterns'
    }
  ];
}
