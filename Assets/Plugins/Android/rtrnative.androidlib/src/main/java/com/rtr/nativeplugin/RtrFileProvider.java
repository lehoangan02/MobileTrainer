package com.rtr.nativeplugin;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.util.Base64;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.nio.charset.StandardCharsets;

/**
 * Serves files that live under the app's private files dirs, so the camera app can write a recording
 * into them and other apps can read them when sharing. Uri form: content://{pkg}.rtrfiles/f/{base64url(absolute path)}/{file name}
 */
public class RtrFileProvider extends ContentProvider {

    public static String authority(Context c) {
        return c.getPackageName() + ".rtrfiles";
    }

    public static Uri uriFor(Context c, File f) {
        String enc = Base64.encodeToString(f.getAbsolutePath().getBytes(StandardCharsets.UTF_8),
                Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
        return new Uri.Builder().scheme("content").authority(authority(c))
                .appendPath("f").appendPath(enc).appendPath(f.getName()).build();
    }

    private File resolve(Uri uri) throws FileNotFoundException {
        java.util.List<String> seg = uri.getPathSegments();
        if (seg.size() < 2 || !"f".equals(seg.get(0))) throw new FileNotFoundException("bad uri");
        try {
            String p = new String(Base64.decode(seg.get(1), Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING),
                    StandardCharsets.UTF_8);
            File f = new File(p).getCanonicalFile();
            Context c = getContext();
            if (isUnder(f, c.getFilesDir()) || isUnder(f, c.getExternalFilesDir(null))) return f;
        } catch (IOException | IllegalArgumentException e) {
            throw new FileNotFoundException(String.valueOf(e.getMessage()));
        }
        throw new FileNotFoundException("path not allowed");
    }

    private static boolean isUnder(File f, File root) throws IOException {
        if (root == null) return false;
        String r = root.getCanonicalPath() + File.separator;
        return f.getPath().startsWith(r);
    }

    @Override public boolean onCreate() { return true; }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        File f = resolve(uri);
        int m = ParcelFileDescriptor.parseMode(mode);
        if (mode.contains("w") && f.getParentFile() != null) f.getParentFile().mkdirs();
        return ParcelFileDescriptor.open(f, m);
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sort) {
        File f;
        try { f = resolve(uri); } catch (FileNotFoundException e) { return null; }
        String[] cols = projection != null ? projection : new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE};
        MatrixCursor cur = new MatrixCursor(cols);
        Object[] row = new Object[cols.length];
        for (int i = 0; i < cols.length; i++) {
            if (OpenableColumns.DISPLAY_NAME.equals(cols[i])) row[i] = f.getName();
            else if (OpenableColumns.SIZE.equals(cols[i])) row[i] = f.length();
        }
        cur.addRow(row);
        return cur;
    }

    @Override public String getType(Uri uri) { return "video/mp4"; }
    @Override public Uri insert(Uri uri, ContentValues v) { throw new UnsupportedOperationException(); }
    @Override public int delete(Uri uri, String s, String[] a) { throw new UnsupportedOperationException(); }
    @Override public int update(Uri uri, ContentValues v, String s, String[] a) { throw new UnsupportedOperationException(); }
}
