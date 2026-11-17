package org.scieport.codex;
import java.io.*;

public final class PickerCoreTests {
    static int checks;
    static void check(boolean ok) { checks++; if(!ok) throw new AssertionError(); }
    static void invalid(String name) {
        try { CodexDocumentPicker.validateName(name); throw new AssertionError(name); }
        catch(IllegalArgumentException expected) { checks++; }
    }
    public static void main(String[] args) throws Exception {
        for(String name:new String[]{"书籍.txt","中文图书.EPUB","a b.txt","1.epub","星空😀.txt"}) {
            CodexDocumentPicker.validateName(name); checks++;
        }
        for(String name:new String[]{null,"",".txt","../书.txt","a/b.txt","a\\b.epub","a:book.txt","book.pdf","book.txt.tmp","a\u0000.txt","a\n.txt","a\u0085.txt","a?.txt"}) invalid(name);
        invalid(new String(new char[151]).replace('\0','a')+".txt");
        byte[] data="测试书籍内容".getBytes("UTF-8");ByteArrayOutputStream out=new ByteArrayOutputStream();
        check(CodexDocumentPicker.copyLimited(new ByteArrayInputStream(data),out,0)==data.length);
        check(java.util.Arrays.equals(data,out.toByteArray()));
        try {CodexDocumentPicker.copyLimited(new ByteArrayInputStream(new byte[0]),out,0);throw new AssertionError();}
        catch(IllegalArgumentException expected){checks++;}
        try {CodexDocumentPicker.copyLimited(new ByteArrayInputStream(data),out,1);throw new AssertionError();}
        catch(InterruptedIOException expected){checks++;}
        final int limit=8*1024*1024;
        OutputStream sink=new OutputStream(){public void write(int b){}public void write(byte[] b,int o,int n){}};
        check(CodexDocumentPicker.copyLimited(new ByteArrayInputStream(new byte[limit]),sink,0)==limit);
        try {CodexDocumentPicker.copyLimited(new ByteArrayInputStream(new byte[limit+1]),sink,0);throw new AssertionError();}
        catch(IllegalArgumentException expected){checks++;}
        System.out.println("Document picker JVM filename/stream/size/cancel checks passed: "+checks);
    }
}
