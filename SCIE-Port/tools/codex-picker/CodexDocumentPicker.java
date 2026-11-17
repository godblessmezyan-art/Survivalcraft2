package org.scieport.codex;

import android.app.Activity;
import android.app.Application;
import android.app.Fragment;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.IOException;
import java.io.InterruptedIOException;
import java.util.UUID;

/** Original bridge for the trimmed x25 Android host. No Activity manifest entry or AndroidX dependency. */
public final class CodexDocumentPicker extends Fragment {
    private static final int REQUEST = 17220;
    private static final int LIMIT = 8 * 1024 * 1024;
    private static volatile int state;
    private static volatile int generation;
    private static volatile String path;
    private static volatile String error;
    private static CodexDocumentPicker active;
    private static ActivityGuard guard;
    private int ticket;

    public static int getState() { return state; }
    public static String getPath() { return path; }
    public static String getError() { return error; }

    public static synchronized void start(final Activity activity) {
        if (state == 1) throw new IllegalStateException("选择器已打开");
        path = null; error = null; state = 1;
        final int next = ++generation;
        activity.runOnUiThread(new Runnable() {
            public void run() {
                if (next != generation) return;
                try {
                    if (!onlyPicker(activity)) throw new IllegalStateException("Native fragment host is busy");
                    guard = new ActivityGuard(activity);
                    activity.getApplication().registerActivityLifecycleCallbacks(guard);
                    CodexDocumentPicker picker = new CodexDocumentPicker();
                    picker.ticket = next; active = picker;
                    activity.getFragmentManager().beginTransaction()
                        .add(picker, "SCIEPort.CodexPicker").commitAllowingStateLoss();
                } catch (Exception failure) { fail(next, "无法打开系统文件选择器"); }
            }
        });
    }

    public static synchronized void cancel() {
        ++generation; state = 3;
        String selected = path; path = null;
        if (selected != null) {
            File file = new File(selected); file.delete(); file.getParentFile().delete();
        }
        final CodexDocumentPicker picker = active;
        if (picker != null && picker.getActivity() != null) {
            picker.getActivity().runOnUiThread(new Runnable() {
                public void run() { picker.detach(); }
            });
        } else if (guard != null) guard.owner.runOnUiThread(guard);
    }

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        setRetainInstance(true);
        if (saved != null) { fail(generation, "选择已中断，请重新选择"); detach(); return; }
        try {
            Intent intent = new Intent("android.intent.action.OPEN_DOCUMENT");
            intent.addCategory("android.intent.category.OPENABLE");
            intent.setType("*/*");
            intent.putExtra("android.intent.extra.MIME_TYPES", new String[] {
                "text/plain", "application/epub+zip", "application/octet-stream"
            });
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivityForResult(intent, REQUEST);
        } catch (Exception failure) { fail(ticket, "系统未提供文件选择器"); detach(); }
    }

    @Override public void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST || ticket != generation) return;
        if (result != Activity.RESULT_OK || data == null || data.getData() == null) {
            state = 3; detach(); return;
        }
        final Uri uri = data.getData();
        final Activity activity = getActivity();
        final int current = ticket;
        new Thread(new Runnable() {
            public void run() { copy(activity, uri, current); }
        }, "CodexDocumentImport").start();
        detach();
    }

    private void detach() {
        try {
            if (getFragmentManager() != null)
                getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        } catch (Exception ignored) { }
        if (active == this) active = null;
        if (guard != null) { guard.release(); guard = null; }
    }

    private static boolean onlyPicker(Activity activity) {
        try {
            Object result;
            try {
                result = android.app.FragmentManager.class.getMethod("getFragments").invoke(activity.getFragmentManager());
            } catch (NoSuchMethodException legacy) {
                if (android.os.Build.VERSION.SDK_INT >= 26) return false;
                // Android 6/7 AOSP uses this ArrayList before the public API was introduced.
                java.lang.reflect.Field field = activity.getFragmentManager().getClass().getDeclaredField("mActive");
                field.setAccessible(true);
                result = field.get(activity.getFragmentManager());
            }
            if (result == null) return true;
            for (Object fragment : (java.util.List<?>)result)
                if (fragment != null && !(fragment instanceof CodexDocumentPicker)) return false;
            return true;
        } catch (Exception unsupported) { return false; }
    }

    // A dynamically loaded Fragment class cannot be resolved by a fresh APK process before mods load.
    // Keep the live result callback, but exclude our sole fragment from saved Activity restoration.
    private static final class ActivityGuard implements Application.ActivityLifecycleCallbacks, Runnable {
        private final Activity owner;
        ActivityGuard(Activity owner) { this.owner = owner; }
        void release() { owner.getApplication().unregisterActivityLifecycleCallbacks(this); }
        public void run() {
            release();
            if (guard != this) return;
            if (active != null) active.detach();
            else guard = null;
        }
        public void onActivitySaveInstanceState(Activity activity, Bundle saved) {
            if (activity == owner && active != null && onlyPicker(activity)) saved.remove("android:fragments");
        }
        public void onActivityCreated(Activity a, Bundle b) { }
        public void onActivityStarted(Activity a) { }
        public void onActivityResumed(Activity a) { }
        public void onActivityPaused(Activity a) { }
        public void onActivityStopped(Activity a) { }
        public void onActivityDestroyed(Activity a) { if (a == owner) release(); }
    }

    private static synchronized void fail(int current, String message) {
        if (current != generation) return;
        error = message; state = 4;
        if (guard != null) guard.owner.runOnUiThread(guard);
    }

    private static void copy(Activity activity, Uri uri, int current) {
        File folder = null, file = null;
        boolean published = false;
        try {
            String name = null;
            Cursor cursor = activity.getContentResolver().query(uri, new String[] {"_display_name"}, null, null, null);
            if (cursor != null) try {
                if (cursor.moveToFirst()) name = cursor.getString(0);
            } finally { cursor.close(); }
            if (name != null) name = name.trim();
            validateName(name);
            File root = new File(activity.getCacheDir(), "CelestialCodexPicker");
            folder = new File(root, UUID.randomUUID().toString());
            if (!folder.mkdirs()) throw new IllegalStateException("缓存无法创建");
            file = new File(folder, name);
            InputStream input = activity.getContentResolver().openInputStream(uri);
            if (input == null) throw new IllegalStateException("文件无法读取");
            try {
                FileOutputStream output = new FileOutputStream(file);
                try {
                    copyLimited(input, output, current);
                } finally { output.close(); }
            } finally { input.close(); }
            synchronized (CodexDocumentPicker.class) {
                if (current != generation) return;
                path = file.getAbsolutePath(); state = 2; published = true;
            }
        } catch (IllegalArgumentException failure) { fail(current, failure.getMessage()); }
          catch (Exception failure) { fail(current, "文件读取失败，请重新选择"); }
        finally {
            if (!published) {
                if (file != null) file.delete();
                if (folder != null) folder.delete();
            }
        }
    }

    static int copyLimited(InputStream input, OutputStream output, int current) throws IOException {
        byte[] buffer = new byte[8192]; int count, total = 0;
        while ((count = input.read(buffer)) != -1) {
            if (current != generation) throw new InterruptedIOException("选择已取消");
            if (count > LIMIT - total) throw new IllegalArgumentException("图书超过8 MiB");
            output.write(buffer, 0, count); total += count;
        }
        if (current != generation) throw new InterruptedIOException("选择已取消");
        if (total == 0) throw new IllegalArgumentException("文件内容为空");
        return total;
    }

    static void validateName(String name) {
        if (name == null || name.length() == 0 || name.length() > 150 || name.startsWith("."))
            throw new IllegalArgumentException("文件名无效");
        String lower = name.toLowerCase(java.util.Locale.ROOT);
        if (!lower.endsWith(".txt") && !lower.endsWith(".epub"))
            throw new IllegalArgumentException("请选择TXT或EPUB");
        for (int i = 0; i < name.length(); i++) {
            char c = name.charAt(i);
            if (Character.isISOControl(c) || "/\\:<>|\"?*".indexOf(c) >= 0)
                throw new IllegalArgumentException("文件名无效");
        }
    }
}
