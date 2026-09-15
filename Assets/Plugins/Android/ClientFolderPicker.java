package com.mobileuo.importer;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.content.ContentResolver;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;
import org.json.JSONObject;
import java.io.*;
import java.util.*;

/** One-shot SAF folder import. Never resolves content URIs to guessed filesystem paths. */
public final class ClientFolderPicker {
    private static volatile boolean cancelled;
    private static volatile boolean busy;
    private static volatile String operation;
    private static volatile String progress = "{\"state\":\"idle\"}";
    private static final Set<String> EXTENSIONS = new HashSet<>(Arrays.asList(
        "mul", "uop", "idx", "def", "enu", "rle", "txt", "cfg", "mp3", "ogg", "wav", "mid", "midi"));
    private static final class Entry {
        Uri uri; String name; boolean directory; long size;
        Entry(Uri uri, String name, boolean directory, long size) {
            this.uri = uri; this.name = name; this.directory = directory; this.size = size;
        }
    }

    public static String status() { return progress; }
    public static void cancel() { cancelled = true; }
    public static synchronized void begin(final Activity activity, final String destination) {
        if (busy) throw new IllegalStateException("A client import is already running.");
        busy = true; cancelled = false; report("waiting", "Choose the folder containing your UO assets.", 0, 0);
        operation = UUID.randomUUID().toString();
        final String request = operation;
        activity.runOnUiThread(() -> {
            try {
                PickerFragment fragment = new PickerFragment();
                Bundle arguments = new Bundle(); arguments.putString("destination", destination);
                arguments.putString("operation", request); fragment.setArguments(arguments);
                activity.getFragmentManager().beginTransaction().add(fragment, "MobileUOFolderPicker").commit();
            } catch (Exception e) { finish("error", e.getMessage()); }
        });
    }

    public static class PickerFragment extends Fragment {
        @Override public void onCreate(Bundle state) {
            super.onCreate(state);
            setRetainInstance(true);
            if (state == null) {
                try {
                    Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
                    intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                    startActivityForResult(intent, 4813);
                } catch (Exception e) { finish("error", "Folder picker unavailable: " + e.getMessage()); remove(); }
            }
        }
        @Override public void onActivityResult(int request, int result, Intent data) {
            if (request != 4813) return;
            // Ignore a picker restored after the app process was killed.
            if (!Objects.equals(operation, getArguments().getString("operation"))) { remove(); return; }
            if (cancelled || result != Activity.RESULT_OK || data == null || data.getData() == null) {
                finish("cancelled", "Import cancelled."); remove(); return;
            }
            final ContentResolver resolver = getActivity().getApplicationContext().getContentResolver();
            final Uri tree = data.getData();
            final String destination = getArguments().getString("destination");
            new Thread(() -> copy(resolver, tree, new File(destination)), "UO client import").start();
            remove();
        }
        private void remove() {
            if (getFragmentManager() != null) getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        }
    }

    private static void copy(ContentResolver resolver, Uri tree, File destination) {
        try {
            if (destination.exists()) throw new IOException("Import destination already exists.");
            Uri root = DocumentsContract.buildDocumentUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
            List<Entry> top = children(resolver, tree, root);
            Set<String> names = new HashSet<>();
            for (Entry entry : top) if (!entry.directory) names.add(entry.name.toLowerCase(Locale.ROOT));
            boolean animation = names.contains("anim.mul") || names.contains("animationframe1.uop");
            boolean art = (names.contains("art.mul") && names.contains("artidx.mul")) || names.contains("artlegacymul.uop");
            if (!animation || !art || !names.contains("tiledata.mul"))
                throw new IOException("Choose the client asset folder containing anim.mul (or animationFrame1.uop), art files, and tiledata.mul.");
            for (Entry entry : top) {
                String name = entry.name.toLowerCase(Locale.ROOT);
                if (entry.size == 0 && (name.equals("anim.mul") || name.equals("animationframe1.uop") || name.equals("tiledata.mul")))
                    throw new IOException("A required client file is empty: " + entry.name);
            }
            report("scanning", "Checking client files and storage…", 0, 0);
            LinkedHashMap<String, Entry> files = new LinkedHashMap<>();
            collect(resolver, tree, top, "", files, new HashSet<String>(), 0);
            long total = 0;
            for (Entry entry : files.values()) total = Math.addExact(total, Math.max(0, entry.size));
            if (!destination.mkdirs()) throw new IOException("Cannot create import staging folder.");
            if (destination.getUsableSpace() < total + 64L * 1024 * 1024)
                throw new IOException("Not enough storage for this client import.");
            long copied = 0, lastReport = 0;
            byte[] buffer = new byte[256 * 1024];
            for (Map.Entry<String, Entry> item : files.entrySet()) {
                checkCancelled();
                File target = new File(destination, item.getKey());
                String base = destination.getCanonicalPath() + File.separator;
                if (!target.getCanonicalPath().startsWith(base)) throw new IOException("Invalid client filename.");
                if (!target.getParentFile().isDirectory() && !target.getParentFile().mkdirs())
                    throw new IOException("Cannot create client subfolder.");
                long written = 0;
                try (InputStream input = resolver.openInputStream(item.getValue().uri);
                     FileOutputStream output = new FileOutputStream(target)) {
                    if (input == null) throw new IOException("Cannot read " + item.getKey());
                    int length;
                    while ((length = input.read(buffer)) != -1) {
                        checkCancelled(); output.write(buffer, 0, length); copied += length; written += length;
                        long now = android.os.SystemClock.elapsedRealtime();
                        if (now - lastReport >= 200) {
                            report("copying", item.getKey(), copied, total); lastReport = now;
                        }
                    }
                    output.getFD().sync();
                }
                if (item.getValue().size >= 0 && written != item.getValue().size)
                    throw new IOException("File changed or was truncated while importing: " + item.getKey());
            }
            checkCancelled();
            finish("complete", "Client files ready. Save the configuration to finish importing.");
        } catch (Exception e) {
            delete(destination);
            finish(cancelled ? "cancelled" : "error", cancelled ? "Import cancelled." : e.getMessage());
        }
    }

    private static List<Entry> children(ContentResolver resolver, Uri tree, Uri parent) throws IOException {
        checkCancelled();
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, DocumentsContract.getDocumentId(parent));
        List<Entry> entries = new ArrayList<>();
        String[] columns = { DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME,
            DocumentsContract.Document.COLUMN_MIME_TYPE, DocumentsContract.Document.COLUMN_SIZE };
        try (Cursor cursor = resolver.query(children, columns, null, null, null)) {
            if (cursor == null) throw new IOException("Cannot list the selected folder.");
            while (cursor.moveToNext()) {
                checkCancelled();
                String name = cursor.getString(1);
                if (name == null || name.equals(".") || name.equals("..") || name.contains("/") || name.contains("\\"))
                    throw new IOException("The selected folder contains an invalid filename.");
                entries.add(new Entry(DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0)), name,
                    DocumentsContract.Document.MIME_TYPE_DIR.equals(cursor.getString(2)), cursor.isNull(3) ? -1 : cursor.getLong(3)));
            }
        }
        return entries;
    }

    private static void collect(ContentResolver resolver, Uri tree, List<Entry> entries, String prefix,
                                Map<String, Entry> files, Set<String> paths, int depth) throws IOException {
        if (depth > 32) throw new IOException("Client folder nesting is too deep.");
        for (Entry entry : entries) {
            checkCancelled();
            String name = entry.name.toLowerCase(Locale.ROOT);
            // Data belongs to MobileUO's profiles; executable files are not needed by this native client.
            if (name.startsWith(".") || (prefix.isEmpty() && (name.equals("data") || name.equals("profiles") || name.equals("screenshots")))) continue;
            String path = prefix + entry.name;
            if (!paths.add(path.toLowerCase(Locale.ROOT))) throw new IOException("Duplicate client filename: " + path);
            if (paths.size() > 100000) throw new IOException("Too many files in the selected folder.");
            if (entry.directory) collect(resolver, tree, children(resolver, tree, entry.uri), path + "/", files, paths, depth + 1);
            else if (EXTENSIONS.contains(name.substring(name.lastIndexOf('.') + 1))) files.put(path, entry);
        }
    }

    private static void checkCancelled() throws IOException { if (cancelled) throw new IOException("Import cancelled."); }
    private static void report(String state, String message, long copied, long total) {
        try {
            progress = new JSONObject().put("state", state).put("message", message == null ? state : message)
                .put("copied", copied).put("total", total).toString();
        } catch (Exception ignored) { }
    }
    private static synchronized void finish(String state, String message) { report(state, message, 0, 0); busy = false; }
    private static void delete(File file) {
        File[] children = file.listFiles();
        if (children != null) for (File child : children) delete(child);
        if (file.exists()) file.delete();
    }
}
