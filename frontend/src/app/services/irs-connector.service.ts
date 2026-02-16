import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class IRSConnectorService {

  constructor(private http: HttpClient) { }

  getMode(): Observable<boolean> {
    return this.http.get<boolean>('/api/irs-connector/mode');
  }

  setMode(useRealApis: boolean): Observable<any> {
    return this.http.post('/api/irs-connector/mode', useRealApis);
  }
}