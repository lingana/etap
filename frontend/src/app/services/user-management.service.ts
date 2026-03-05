import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { environment } from '../../environments/environment';

export interface TeamMember {
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
  assignedCases: number;
  reviewedTransactions: number;
}

export interface InviteUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  role: string;
}

export interface UpdateUserRoleRequest {
  role: string;
}

@Injectable({
  providedIn: 'root'
})
export class UserManagementService {
  private apiUrl = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getTeamMembers(): Observable<TeamMember[]> {
    return this.http.get<TeamMember[]>(this.apiUrl);
  }

  getUserById(id: number): Observable<TeamMember> {
    return this.http.get<TeamMember>(`${this.apiUrl}/${id}`);
  }

  inviteUser(request: InviteUserRequest): Observable<TeamMember> {
    return this.http.post<TeamMember>(`${this.apiUrl}/invite`, request);
  }

  updateUserRole(id: number, request: UpdateUserRoleRequest): Observable<TeamMember> {
    return this.http.put<TeamMember>(`${this.apiUrl}/${id}/role`, request);
  }

  deactivateUser(id: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/deactivate`, {});
  }

  activateUser(id: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/activate`, {});
  }

  getUserActivity(userId: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/${userId}/activity`);
  }
}
