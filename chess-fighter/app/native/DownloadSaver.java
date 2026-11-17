package app.chessfighter.game;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Base64;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStream;

/** Saves a file (sent as base64) into the phone's public Downloads folder, where the Files app and the gallery can see it. */
@CapacitorPlugin(name = "DownloadSaver")
public class DownloadSaver extends Plugin {
    @PluginMethod
    public void save(PluginCall call) {
        String name = call.getString("name");
        String data = call.getString("data");
        String mime = call.getString("mime", "video/mp4");
        if (name == null || data == null) {
            call.reject("name and data are required");
            return;
        }
        try {
            byte[] bytes = Base64.decode(data, Base64.DEFAULT);
            JSObject result = new JSObject();
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                // Android 10 and later: add the file through MediaStore (no storage permission needed).
                ContentResolver resolver = getContext().getContentResolver();
                ContentValues values = new ContentValues();
                values.put(MediaStore.MediaColumns.DISPLAY_NAME, name);
                values.put(MediaStore.MediaColumns.MIME_TYPE, mime);
                values.put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS);
                values.put(MediaStore.MediaColumns.IS_PENDING, 1);
                Uri uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values);
                if (uri == null) {
                    call.reject("Could not create the file");
                    return;
                }
                try (OutputStream out = resolver.openOutputStream(uri)) {
                    out.write(bytes);
                }
                values.clear();
                values.put(MediaStore.MediaColumns.IS_PENDING, 0);
                resolver.update(uri, values, null, null);
                result.put("uri", uri.toString());
            } else {
                // Android 9 and earlier: write to the public Downloads folder (needs the storage permission; the page falls back to the share sheet if it is refused).
                File dir = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS);
                dir.mkdirs();
                File file = new File(dir, name);
                try (FileOutputStream out = new FileOutputStream(file)) {
                    out.write(bytes);
                }
                result.put("uri", Uri.fromFile(file).toString());
            }
            call.resolve(result);
        } catch (Exception e) {
            call.reject(e.getMessage() == null ? "Could not save the file" : e.getMessage());
        }
    }
}
