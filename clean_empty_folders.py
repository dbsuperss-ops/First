import os
import tkinter as tk
from tkinter import filedialog, messagebox

# 보호할 시스템 폴더 이름 목록 (소문자로 작성)
SYSTEM_FOLDERS = {
    'windows', 'program files', 'program files (x86)', 'programdata',
    'system volume information', '$recycle.bin', 'recovery',
    'appdata', 'application data', 'local settings'
}

def is_system_path(dirpath):
    """경로 내에 시스템 폴더나 보호할 폴더가 포함되어 있는지 확인합니다."""
    parts = os.path.normpath(dirpath).split(os.sep)
    for part in parts:
        # 지정된 시스템 폴더이거나 '$'로 시작하는 숨김/시스템 폴더인 경우
        if part.lower() in SYSTEM_FOLDERS or part.startswith('$'):
            return True
    return False

def remove_empty_folders(path):
    deleted_count = 0
    
    # topdown=False: 가장 깊숙한 하위 폴더부터 탐색해야 
    # 자식 폴더가 삭제된 후 비어있게 된 부모 폴더도 삭제할 수 있습니다.
    for dirpath, dirnames, filenames in os.walk(path, topdown=False):
        # 시스템 경로는 스킵
        if is_system_path(dirpath):
            continue
            
        try:
            # 폴더 내에 항목이 하나도 없는지 확인
            if not os.listdir(dirpath):
                try:
                    os.rmdir(dirpath)
                    print(f"삭제됨: {dirpath}")
                    deleted_count += 1
                except PermissionError:
                    # 삭제하려 했으나 권한이 없는 경우 조용히 무시
                    print(f"[건너뜀] 삭제 권한 없음: {dirpath}")
                except OSError as e:
                    print(f"[실패] {dirpath}: {e}")
        except PermissionError:
            # os.listdir() 단계에서 접근 거부(WinError 5)가 발생하는 경우를 처리
            print(f"[건너뜀] 접근 거부됨 (WinError 5): {dirpath}")
            
    return deleted_count

def main():
    root = tk.Tk()
    root.withdraw()
    
    target_dir = filedialog.askdirectory(title="빈 폴더를 검색할 최상위 폴더 선택")
    
    if target_dir:
        confirm = messagebox.askyesno(
            "빈 폴더 삭제 확인", 
            f"다음 경로 내의 모든 빈 폴더를 삭제하시겠습니까?\n\n{target_dir}\n\n(※ 시스템 폴더 및 접근 불가 폴더는 안전하게 제외됩니다.)"
        )
        
        if confirm:
            count = remove_empty_folders(target_dir)
            messagebox.showinfo("작업 완료", f"작업이 완료되었습니다.\n총 {count}개의 빈 폴더가 삭제되었습니다.")
        else:
            messagebox.showinfo("취소", "작업이 취소되었습니다.")
    else:
        print("폴더 선택이 취소되었습니다.")

if __name__ == "__main__":
    main()
