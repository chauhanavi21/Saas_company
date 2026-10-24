import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export type TaskStatus = 'Todo' | 'InProgress' | 'Done';

export interface Project {
  id: string;
  name: string;
  description: string | null;
  isArchived: boolean;
  taskCount: number;
  doneCount: number;
  createdAtUtc: string;
}

export interface ProjectTask {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  assigneeUserId: string | null;
  dueDate: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/projects`;

  list(includeArchived = false) {
    return this.http.get<Project[]>(this.base, { params: { includeArchived } });
  }
  get(id: string) {
    return this.http.get<Project>(`${this.base}/${id}`);
  }
  create(name: string, description: string | null) {
    return this.http.post<Project>(this.base, { name, description });
  }
  update(id: string, name: string, description: string | null) {
    return this.http.put<Project>(`${this.base}/${id}`, { name, description });
  }
  archive(id: string) {
    return this.http.post<void>(`${this.base}/${id}/archive`, {});
  }
  unarchive(id: string) {
    return this.http.post<void>(`${this.base}/${id}/unarchive`, {});
  }
  delete(id: string) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  tasks(projectId: string) {
    return this.http.get<ProjectTask[]>(`${this.base}/${projectId}/tasks`);
  }
  createTask(projectId: string, title: string, assigneeUserId: string | null, dueDate: string | null) {
    return this.http.post<ProjectTask>(`${this.base}/${projectId}/tasks`, {
      title,
      description: null,
      assigneeUserId,
      dueDate,
    });
  }
  updateTask(projectId: string, task: ProjectTask, changes: Partial<ProjectTask>) {
    const t = { ...task, ...changes };
    return this.http.put<ProjectTask>(`${this.base}/${projectId}/tasks/${task.id}`, {
      title: t.title,
      description: t.description,
      status: t.status,
      assigneeUserId: t.assigneeUserId,
      dueDate: t.dueDate,
    });
  }
  deleteTask(projectId: string, taskId: string) {
    return this.http.delete<void>(`${this.base}/${projectId}/tasks/${taskId}`);
  }
}
