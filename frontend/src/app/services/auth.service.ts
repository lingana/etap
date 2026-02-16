import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';
import { of } from 'rxjs';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  user?: UserDto;
  token?: string;
}

export interface UserDto {
  id: number;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  role: string;
  roleName: string;
  isActive: boolean;
  lastLogin?: Date;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = 'http://localhost:5000/api/auth';
  private currentUserSubject = new BehaviorSubject<UserDto | null>(null);
  private tokenSubject = new BehaviorSubject<string | null>(null);

  public currentUser$ = this.currentUserSubject.asObservable();
  public token$ = this.tokenSubject.asObservable();
  public isAuthenticated$ = this.currentUser$.pipe(
    // isAuthenticated is true if user is not null
  );

  constructor(private http: HttpClient) {
    this.loadStoredUser();
  }

  private loadStoredUser(): void {
    const token = localStorage.getItem('auth_token');
    const user = localStorage.getItem('current_user');

    if (token) {
      this.tokenSubject.next(token);
    }

    if (user) {
      try {
        const userData = JSON.parse(user);
        this.currentUserSubject.next(userData);
      } catch (e) {
        console.error('Failed to parse stored user:', e);
        localStorage.removeItem('current_user');
      }
    }
  }

  login(username: string, password: string): Observable<LoginResponse> {
    const request: LoginRequest = { username, password };
    
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, request)
      .pipe(
        tap(response => {
          if (response.success && response.user && response.token) {
            // Store token and user
            localStorage.setItem('auth_token', response.token);
            localStorage.setItem('current_user', JSON.stringify(response.user));
            
            // Update subjects
            this.tokenSubject.next(response.token);
            this.currentUserSubject.next(response.user);
          }
        }),
        catchError(error => {
          console.error('Login error:', error);
          throw error;
        })
      );
  }

  logout(): void {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('current_user');
    this.currentUserSubject.next(null);
    this.tokenSubject.next(null);
  }

  getCurrentUser(): UserDto | null {
    return this.currentUserSubject.value;
  }

  getToken(): string | null {
    return this.tokenSubject.value;
  }

  isAuthenticated(): boolean {
    return this.currentUserSubject.value !== null;
  }

  hasRole(role: string): boolean {
    const user = this.currentUserSubject.value;
    return user ? user.role === role : false;
  }

  isManager(): boolean {
    return this.hasRole('Manager');
  }

  isAuditor(): boolean {
    return this.hasRole('Auditor');
  }

  isAdmin(): boolean {
    return this.hasRole('Admin');
  }
}
