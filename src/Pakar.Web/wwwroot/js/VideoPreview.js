// Admin-authorized fetch. No bearer tokens in media URLs or static public directories.
window.pakarVideo = (() => {
    const active = new Map();
    function dispose(id){const previous=active.get(id);if(!previous)return;previous.controller.abort();previous.observer.disconnect();if(previous.url)URL.revokeObjectURL(previous.url);previous.video.pause();previous.video.removeAttribute('src');previous.video.load();active.delete(id);}
    async function load(video,id,path,token){
        dispose(id);if(!video)return null;const state={video,controller:new AbortController(),url:null,observer:new MutationObserver(()=>{if(!video.isConnected)dispose(id);})};state.observer.observe(document.body,{childList:true,subtree:true});active.set(id,state);
        try{
            const response=await fetch(path,{headers:{Authorization:'Bearer '+token},cache:'no-store',signal:state.controller.signal});
            if(!response.ok)return ({401:'Sesi berakhir. Silakan masuk kembali.',403:'Pratinjau hanya tersedia untuk admin.',404:'File video belum tersedia di server.',413:'Klip pratinjau melebihi batas 50 MB.',503:'Pratinjau video belum dikonfigurasi di server.'})[response.status]||'Pratinjau video belum dapat dimuat.';
            const blob=await response.blob();if(blob.size>50*1024*1024)return 'Klip pratinjau melebihi batas 50 MB.';
            if(active.get(id)!==state)return null;
            state.url=URL.createObjectURL(blob);video.src=state.url;video.load();return null;
        }catch(error){return error.name==='AbortError'?null:'Server video belum dapat dihubungi. Coba lagi.';}
    }
    return {load,dispose};
})();
