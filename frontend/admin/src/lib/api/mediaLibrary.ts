import { api as fetchApi } from './core';
import type { MediaFolderDto, MediaAssetDto, CourseMediaDto } from './types';

// --- Media Folders ---
export const getFolders = async (parentFolderId?: string): Promise<MediaFolderDto[]> => {
    let url = '/media-folders';
    if (parentFolderId) {
        url += `?parentFolderId=${parentFolderId}`;
    }
    return fetchApi(url);
};

export const getFolder = async (id: string): Promise<MediaFolderDto> => {
    return fetchApi(`/media-folders/${id}`);
};

export const createFolder = async (data: { name: string; parentFolderId?: string }): Promise<MediaFolderDto> => {
    return fetchApi('/media-folders', {
        method: 'POST',
        body: JSON.stringify(data),
    });
};

export const updateFolder = async (id: string, data: { name: string; parentFolderId?: string }): Promise<MediaFolderDto> => {
    return fetchApi(`/media-folders/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
    });
};

export const deleteFolder = async (id: string, force: boolean = false): Promise<void> => {
    return fetchApi(`/media-folders/${id}${force ? '?force=true' : ''}`, {
        method: 'DELETE',
    });
};

export const getAssets = async (folderId?: string): Promise<MediaAssetDto[]> => {
    let url = '/media/assets?pageSize=100';
    if (folderId) {
        url += `&folderId=${folderId}`;
    }
    const res = await fetchApi(url);
    return res.items || [];
};

export const getAsset = async (id: string): Promise<MediaAssetDto> => {
    return fetchApi(`/media/${id}`);
};

export interface TranscodeProgress { percentage: number; speed: number; etaSeconds: number; }
export const getTranscodeProgress = async (ids: string[]): Promise<Record<string, TranscodeProgress>> => {
    if (!ids || ids.length === 0) return {};
    return fetchApi(`/media/transcode-progress?ids=${ids.join(',')}`);
};

export const createAsset = async (data: { title: string; type: string; filePath: string; durationSeconds?: number; folderId?: string | null }): Promise<MediaAssetDto> => {
    return fetchApi('/media/assets', {
        method: 'POST',
        body: JSON.stringify(data),
    });
};

export const updateAsset = async (id: string, data: { title?: string; folderId?: string | null; tags?: string }): Promise<MediaAssetDto> => {
    return fetchApi(`/media/assets/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
    });
};

export const deleteAsset = async (id: string): Promise<void> => {
    return fetchApi(`/media/assets/${id}`, {
        method: 'DELETE',
    });
};

export const getAssetCourses = async (id: string): Promise<string[]> => {
    return fetchApi(`/media/${id}/courses`);
};

// --- Course Media ---
export const getCourseMedias = async (courseId: string): Promise<CourseMediaDto[]> => {
    return fetchApi(`/courses/${courseId}/media`);
};

export const assignMediaToCourse = async (courseId: string, mediaAssetId: string): Promise<CourseMediaDto> => {
    return fetchApi(`/courses/${courseId}/media/assign`, {
        method: 'POST',
        body: JSON.stringify({ mediaAssetId }),
    });
};

export const assignExamToCourse = async (courseId: string, examId: string): Promise<CourseMediaDto> => {
    return fetchApi(`/courses/${courseId}/media/assign-exam`, {
        method: 'POST',
        body: JSON.stringify({ examId }),
    });
};

export const bulkAssignFolderToCourse = async (courseId: string, folderId: string): Promise<void> => {
    return fetchApi(`/courses/${courseId}/media/bulk-assign-folder`, {
        method: 'POST',
        body: JSON.stringify({ folderId }),
    });
};

export const removeMediaFromCourse = async (courseId: string, mediaAssetId: string): Promise<void> => {
    return fetchApi(`/courses/${courseId}/media/${mediaAssetId}`, {
        method: 'DELETE',
    });
};

export const removeItemFromCourse = async (courseId: string, courseMediaId: string): Promise<void> => {
    return fetchApi(`/courses/${courseId}/media/item/${courseMediaId}`, {
        method: 'DELETE',
    });
};

export const reorderCourseMedias = async (courseId: string, courseMediaIds: string[]): Promise<void> => {
    return fetchApi(`/courses/${courseId}/media/reorder`, {
        method: 'POST',
        body: JSON.stringify({ courseMediaIds }),
    });
};

export const mediaLibraryApi = {
    getFolders,
    getFolder,
    createFolder,
    updateFolder,
    deleteFolder,
    getAssets,
    getAsset,
    createAsset,
    updateAsset,
    deleteAsset,
    getAssetCourses,
    getCourseMedias,
    assignMediaToCourse,
    assignExamToCourse,
    bulkAssignFolderToCourse,
    removeMediaFromCourse,
    removeItemFromCourse,
    reorderCourseMedias,
    getTranscodeProgress
};
