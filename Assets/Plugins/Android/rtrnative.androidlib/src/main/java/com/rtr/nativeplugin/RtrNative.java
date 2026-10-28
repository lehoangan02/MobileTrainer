package com.rtr.nativeplugin;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.graphics.Bitmap;
import android.media.MediaMetadataRetriever;
import android.net.Uri;

import java.io.File;
import java.io.FileOutputStream;

/** Entry points called from Unity C# over JNI. */
public final class RtrNative {
    private RtrNative() {}

    public interface Callback {
        void onResult(String status, String path, String message);
    }

    static volatile Callback pending;

    /** Opens the system camera in video mode and writes the recording to destPath. */
    public static void recordVideo(Activity activity, String destPath, Callback cb) {
        pending = cb;
        Intent i = new Intent(activity, RtrCaptureActivity.class);
        i.putExtra(RtrCaptureActivity.EXTRA_DEST, destPath);
        activity.startActivity(i);
    }

    /** Opens the share sheet for a video file. */
    public static void shareVideo(final Activity activity, String path, String title) {
        final File f = new File(path);
        final Uri uri = RtrFileProvider.uriFor(activity, f);
        final Intent send = new Intent(Intent.ACTION_SEND);
        send.setType("video/mp4");
        send.putExtra(Intent.EXTRA_STREAM, uri);
        send.putExtra(Intent.EXTRA_TITLE, title);
        send.setClipData(ClipData.newRawUri("video", uri));
        send.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        final Intent chooser = Intent.createChooser(send, title);
        chooser.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.runOnUiThread(new Runnable() {
            @Override public void run() { activity.startActivity(chooser); }
        });
    }

    /**
     * Reads video metadata and writes a poster frame.
     * @return "durationMs|width|height" or "" on failure.
     */
    public static String probeVideo(String videoPath, String thumbPath) {
        MediaMetadataRetriever r = new MediaMetadataRetriever();
        try {
            r.setDataSource(videoPath);
            String dur = r.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION);
            String w = r.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_WIDTH);
            String h = r.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_HEIGHT);
            long durMs = dur == null ? 0 : Long.parseLong(dur);
            Bitmap bmp = r.getFrameAtTime(Math.min(durMs, 500) * 1000L, MediaMetadataRetriever.OPTION_CLOSEST_SYNC);
            if (bmp != null && thumbPath != null && thumbPath.length() > 0) {
                int tw = 480;
                int th = Math.max(1, bmp.getHeight() * tw / Math.max(1, bmp.getWidth()));
                Bitmap scaled = Bitmap.createScaledBitmap(bmp, tw, th, true);
                try (FileOutputStream out = new FileOutputStream(thumbPath)) {
                    scaled.compress(Bitmap.CompressFormat.JPEG, 80, out);
                }
            }
            return durMs + "|" + w + "|" + h;
        } catch (Exception e) {
            return "";
        } finally {
            try { r.release(); } catch (Exception ignored) {}
        }
    }
}
