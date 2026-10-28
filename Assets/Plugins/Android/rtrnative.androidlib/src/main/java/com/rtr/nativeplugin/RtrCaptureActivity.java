package com.rtr.nativeplugin;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.provider.MediaStore;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Transparent trampoline: launches the system camera in video mode, writes the result to the requested file,
 * and reports back through RtrNative.Callback. Needed because the Unity activity does not forward onActivityResult.
 */
public class RtrCaptureActivity extends Activity {
    static final String EXTRA_DEST = "rtr_dest";
    private static final int REQ_CAPTURE = 4711;

    private File dest;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        String d = getIntent().getStringExtra(EXTRA_DEST);
        if (d == null) { finishWith("error", null, "no destination"); return; }
        dest = new File(d);
        if (saved != null) return; // recreated while the camera is open; result still arrives

        try {
            if (dest.getParentFile() != null) dest.getParentFile().mkdirs();
            Uri out = RtrFileProvider.uriFor(this, dest);
            Intent i = new Intent(MediaStore.ACTION_VIDEO_CAPTURE);
            i.putExtra(MediaStore.EXTRA_OUTPUT, out);
            i.putExtra(MediaStore.EXTRA_VIDEO_QUALITY, 1);
            i.setClipData(ClipData.newRawUri("video", out));
            i.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivityForResult(i, REQ_CAPTURE);
        } catch (ActivityNotFoundException e) {
            finishWith("error", null, "no camera app available");
        } catch (Exception e) {
            finishWith("error", null, String.valueOf(e));
        }
    }

    @Override
    protected void onActivityResult(int req, int result, Intent data) {
        super.onActivityResult(req, result, data);
        if (req != REQ_CAPTURE) return;
        if (result != RESULT_OK) {
            dest.delete();
            finishWith("cancelled", null, "");
            return;
        }
        try {
            if (!dest.exists() || dest.length() == 0) {
                // camera app ignored EXTRA_OUTPUT and returned its own uri
                Uri src = data != null ? data.getData() : null;
                if (src == null) { finishWith("error", null, "camera returned no video"); return; }
                try (InputStream in = getContentResolver().openInputStream(src);
                     OutputStream outS = new FileOutputStream(dest)) {
                    byte[] buf = new byte[1 << 16];
                    int n;
                    while ((n = in.read(buf)) > 0) outS.write(buf, 0, n);
                }
            }
            finishWith("ok", dest.getAbsolutePath(), "");
        } catch (Exception e) {
            finishWith("error", null, String.valueOf(e));
        }
    }

    private void finishWith(String status, String path, String msg) {
        RtrNative.Callback cb = RtrNative.pending;
        RtrNative.pending = null;
        finish();
        if (cb != null) cb.onResult(status, path == null ? "" : path, msg == null ? "" : msg);
    }
}
