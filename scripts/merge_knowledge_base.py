import os
import re
import sys
import json
from collections import defaultdict

# 默认配置
DEFAULT_SOURCE_DIR = r"d:\CS2.WorkSpace\CS2Mod\A.Mod\MapExt2\MapExt2-Eco-2.2.0-build-1.5.3f-260113\Doc"
OUTPUT_DIR = r"d:\CS2.WorkSpace\CS2Mod\A.Mod\MapExt2\MapExt2-Eco-2.2.0-build-1.5.3f-260113\_KnowledgeBase"
MAX_FILE_SIZE_MB = 0.1  # 单个合并文件最大 0.1MB (约 100KB)
OUTPUT_EXT = ".cs.txt"  # 输出文件后缀，避免 IDE 报错

def ensure_dir(directory):
    if not os.path.exists(directory):
        os.makedirs(directory)

def clean_output_dir(directory):
    """清理输出目录中的旧文件"""
    if os.path.exists(directory):
        for f in os.listdir(directory):
            if f.endswith(".cs") or f.endswith(".cs.txt") or f == "_Index.json":
                try:
                    os.remove(os.path.join(directory, f))
                except Exception as e:
                    print(f"Failed to delete {f}: {e}")

def get_namespace(content):
    """从文件内容中提取 namespace"""
    match = re.search(r'namespace\s+([\w\.]+)', content)
    return match.group(1) if match else "Global"

def extract_types(content):
    """提取代码中的类型定义 (class, struct, interface, enum)"""
    # 简单正则匹配，可能无法处理极其复杂的嵌套或注释中的情况，但对反编译代码通常足够
    # 匹配 public/internal/private class/struct/interface/enum Name
    pattern = r'(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*\s+(class|struct|interface|enum)\s+(\w+)'
    matches = re.findall(pattern, content)
    return [name for _, name in matches]

def merge_files(source_dir):
    ensure_dir(OUTPUT_DIR)
    clean_output_dir(OUTPUT_DIR)
    
    # 1. 扫描并分组
    # 结构: { namespace: [ (file_path, content, size), ... ] }
    files_by_namespace = defaultdict(list)
    
    print(f"Scanning source files in: {source_dir}")
    for root, _, files in os.walk(source_dir):
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
    
    global_index = {} # { ClassName: [FilePath, ...] }
    
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
                out_name = f"{ns}{OUTPUT_EXT}"
            else:
                out_name = f"{ns}_Part{i+1}{OUTPUT_EXT}"
                
            out_path = os.path.join(OUTPUT_DIR, out_name)
            
            # 收集所有 using
            all_usings = set()
            merged_body = []
            chunk_types = []
            
            for f_path, content, _ in chunk:
                # 提取 using
                usings = re.findall(r'^\s*using\s+[\w\.=]+;', content, re.MULTILINE)
                all_usings.update(usings)
                
                # 提取类型并更新索引
                types = extract_types(content)
                chunk_types.extend(types)
                for t in types:
                    full_name = f"{ns}.{t}" if ns != "Global" else t
                    if full_name not in global_index:
                        global_index[full_name] = []
                    global_index[full_name].append(out_name)
                    # 同时索引短名，方便搜索
                    if t not in global_index:
                        global_index[t] = []
                    if out_name not in global_index[t]:
                        global_index[t].append(out_name)

                # 提取主体（去除 using）
                body = re.sub(r'^\s*using\s+[\w\.=]+;[\r\n]*', '', content, flags=re.MULTILINE)
                
                file_name = os.path.basename(f_path)
                merged_body.append(f"\n// === Source: {file_name} ===\n")
                merged_body.append(body.strip())
            
            # 构造文件头摘要
            header = f"// Knowledge Base File: {out_name}\n"
            header += f"// Namespace: {ns}\n"
            header += f"// Contains Types: {', '.join(sorted(list(set(chunk_types))))}\n"
            header += "// =======================================================\n\n"
            
            # 组合最终内容
            final_content = header + "\n".join(sorted(list(all_usings))) + "\n\n" + "\n\n".join(merged_body)
            
            with open(out_path, 'w', encoding='utf-8') as f:
                f.write(final_content)
                
            print(f"Generated: {out_name} ({len(chunk)} files, {len(chunk_types)} types)")

    # 3. 写入索引文件
    index_path = os.path.join(OUTPUT_DIR, "_Index.json")
    with open(index_path, 'w', encoding='utf-8') as f:
        json.dump(global_index, f, indent=2)
    print(f"Generated Index: _Index.json ({len(global_index)} entries)")

if __name__ == "__main__":
    target_dir = DEFAULT_SOURCE_DIR
    if len(sys.argv) > 1:
        target_dir = sys.argv[1]
        
    if os.path.exists(target_dir):
        merge_files(target_dir)
    else:
        print(f"Error: Source directory not found: {target_dir}")
