const loads = new Map();

export async function toggleFullscreen(tile) {
    try {
        if (document.fullscreenElement === tile) {
            await document.exitFullscreen();
        } else if (document.fullscreenEnabled) {
            await tile.requestFullscreen();
        } else {
            return false;
        }
        return true;
    } catch {
        return false;
    }
}

export async function load(key, video, url, token) {
    release(key);
    const state = { video, controller: new AbortController(), objectUrl: null };
    loads.set(key, state);
    try {
        const response = await fetch(url, {
            signal: state.controller.signal,
            credentials: 'omit',
            headers: { Authorization: 'Bearer ' + token }
        });
        if (!response.ok) return response.status;
        const blob = await response.blob();
        if (state.controller.signal.aborted) return 0;
        state.objectUrl = URL.createObjectURL(blob);
        video.muted = true;
        video.src = state.objectUrl;
        void video.play().catch(() => { /* Native controls remain available. */ });
        return response.status;
    } catch (error) {
        if (error.name === 'AbortError') return 0;
        throw error;
    }
}

export function release(key) {
    const state = loads.get(key);
    if (!state) return;
    const { video } = state;
    state.controller.abort();
    video.pause();
    video.removeAttribute('src');
    video.load();
    if (state.objectUrl) URL.revokeObjectURL(state.objectUrl);
    loads.delete(key);
}
