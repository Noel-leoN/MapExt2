import os
import re
from collections import defaultdict

# 配置
SOURCE_DIR = r"d:\CS2.WorkSpace\CS2Mod\A.Mod\GameLib"
OUTPUT_DIR = r"d:\CS2.WorkSpace\CS2Mod\A.Mod\_KnowledgeBase"
MAX_FILE_SIZE_MB = 0.5  # 单个合并文件最大 0.5MB (约 500KB)

def ensure_dir(directory):
    if not os.path.exists(directory):
        os.makedirs(directory)

def get_namespace(content):
    """从文件内容中提取 namespace"""
    match = re.search(r'namespace\s+([\w\.]+)', content)
    return match.group(1) if match else "Global"

def merge_files():
    ensure_dir(OUTPUT_DIR)
    
    # 1. 扫描并分组
    # 结构: { namespace: [ (file_path, content, size), ... ] }
    files_by_namespace = defaultdict(list)
    
    print("Scanning source files...")
    for root, _, files in os.walk(SOURCE_DIR):
        for file in files:
            if file.endswith(".cs"):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()
                        
                    ns = get_namespace(content)
                    size = len(content.encode('utf-8'))
                    files_by_namespace[ns].append((file_path, content, size))
                except Exception as e:
                    print(f"Skipping {file}: {e}")

    # 2. 合并并写入
    print(f"Found {len(files_by_namespace)} namespaces. Merging...")
    
    for ns, file_list in files_by_namespace.items():
        # 按文件名排序，保证顺序稳定
        file_list.sort(key=lambda x: x[0])
        
        chunks = []
        current_chunk = []
        current_size = 0
        
        # 分块逻辑
        for item in file_list:
            # 如果当前块加上新文件会超限，且当前块不为空，则先封存当前块
            if current_size + item[2] > MAX_FILE_SIZE_MB * 1024 * 1024 and current_chunk:
                chunks.append(current_chunk)
                current_chunk = []
                current_size = 0
            
            current_chunk.append(item)
            current_size += item[2]
        
        if current_chunk:
            chunks.append(current_chunk)
            
        # 写入文件
        for i, chunk in enumerate(chunks):
            # 构造文件名
            if len(chunks) == 1:
                out_name = f"{ns}.cs"
            else:
                out_name = f"{ns}_Part{i+1}.cs"
                
            out_path = os.path.join(OUTPUT_DIR, out_name)
            
            # 收集所有 using
            all_usings = set()
            merged_body = []
            
            for f_path, content, _ in chunk:
                # 提取 using
                usings = re.findall(r'^\s*using\s+[\w\.=]+;', content, re.MULTILINE)
                all_usings.update(usings)
                
                # 提取主体（去除 using）
                body = re.sub(r'^\s*using\s+[\w\.=]+;[\r\n]*', '', content, flags=re.MULTILINE)
                
                file_name = os.path.basename(f_path)
                merged_body.append(f"\n// === Source: {file_name} ===\n")
                merged_body.append(body.strip())
            
            # 组合最终内容
            final_content = "\n".join(sorted(list(all_usings))) + "\n\n" + "\n\n".join(merged_body)
            
            with open(out_path, 'w', encoding='utf-8') as f:
                f.write(final_content)
                
            print(f"Generated: {out_name} ({len(chunk)} files)")

if __name__ == "__main__":
    merge_files()