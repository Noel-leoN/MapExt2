import sys
import re
import os

def clean_content(content):
    """
    清理 C# 反编译代码中的冗余部分
    """
    # 1. 移除 [CompilerGenerated] 和 [Preserve] 属性
    # 使用 re.MULTILINE 匹配行首，替换为空字符串
    # 匹配模式：空白 + [ + 关键词 + ] + 可能的空白 + 行尾
    content = re.sub(r'^\s*\[(CompilerGenerated|Preserve)\].*?$', '', content, flags=re.MULTILINE)
    
    # 2. 移除空构造函数
    # 匹配模式：public 类名() { 空白 }
    content = re.sub(r'^\s*public\s+\w+\(\)\s*\{[\s\r\n]*\}', '', content, flags=re.MULTILINE)

    # 3. 清理双分号
    content = content.replace(';;', ';')

    # 4. 清理所有空行
    # 将内容按行分割，只保留包含非空白字符的行
    lines = content.splitlines()
    # strip() 移除首尾空白，如果结果非空则保留该行
    # 注意：这样会保留代码缩进吗？
    # 不，line.strip() 只是用于判断。保留的 line 还是原始的 line。
    non_empty_lines = [line for line in lines if line.strip()]
    content = '\n'.join(non_empty_lines)
    
    return content

def process_file(file_path):
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = clean_content(content)
        
        if content != new_content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"[已清理] {file_path}")
        else:
            print(f"[无变化] {file_path}")
            
    except Exception as e:
        print(f"[错误] 处理 {file_path} 失败: {e}")

def main():
    if len(sys.argv) < 2:
        print("用法: python clean_decompiled.py <文件路径 或 目录路径>")
        return

    target = sys.argv[1]
    
    if os.path.isfile(target):
        process_file(target)
    elif os.path.isdir(target):
        print(f"正在扫描目录: {target} ...")
        for root, dirs, files in os.walk(target):
            for file in files:
                if file.endswith(".cs"):
                    process_file(os.path.join(root, file))
    else:
        print(f"错误: 路径不存在 {target}")

if __name__ == "__main__":
    main()
