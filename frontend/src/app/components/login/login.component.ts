import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  @Output() loginSuccess = new EventEmitter<void>();

  loginForm!: FormGroup;
  isLoading = false;
  showPassword = false;
  returnUrl = '/';

  // Demo credentials for quick testing
  demoUsers = [
    { username: 'auditor', password: 'password123', role: 'Auditor' },
    { username: 'manager', password: 'password123', role: 'Manager' },
    { username: 'admin', password: 'password123', role: 'Admin' }
  ];

  constructor(
    private formBuilder: FormBuilder,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    // Redirect to dashboard if already logged in
    if (this.authService.isAuthenticated()) {
      this.loginSuccess.emit();
      return;
    }

    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/tax-selection';

    this.loginForm = this.formBuilder.group({
      username: ['', Validators.required],
      password: ['', Validators.required]
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.toastService.error('Please fill in all fields');
      return;
    }

    this.isLoading = true;
    const { username, password } = this.loginForm.value;

    this.authService.login(username, password).subscribe({
      next: (response) => {
        if (response.success) {
          // Save login time for token expiration tracking
          localStorage.setItem('login_time', Date.now().toString());
          this.toastService.success(`Welcome, ${response.user?.fullName}!`);
          this.loginSuccess.emit(); // Emit event instead of navigating
        } else {
          this.toastService.error(response.message || 'Login failed');
          this.isLoading = false;
        }
      },
      error: (error) => {
        this.isLoading = false;
        this.toastService.error('Invalid username or password');
      }
    });
  }

  fillDemoCredentials(user: any): void {
    this.loginForm.patchValue({
      username: user.username,
      password: user.password
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  get username() {
    return this.loginForm.get('username');
  }

  get password() {
    return this.loginForm.get('password');
  }
}
