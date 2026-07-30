mergeInto(LibraryManager.library, {
    SyncFiles: function () {
        if (typeof FS !== 'undefined') {
            FS.syncfs(false, function (err) {
                if (err) {
                    console.error("WebGL Virtual File System Sync Failed:", err);
                } else {
                    console.log("WebGL Virtual File System Synced Successfully!");
                }
            });
        }
    }
});