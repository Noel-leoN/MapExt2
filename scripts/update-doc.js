const fs = require('fs');
const path = require('path');

const docPath = path.join(__dirname, '../CoreLogicSummary.md');
const srcPath = path.join(__dirname, '../MapExt');

// 1. 更新日期
function updateDate(content) {
    const today = new Date().toISOString().split('T')[0];
    const dateRegex = /(\*\*生成日期\*\*: ).*/;
    if (dateRegex.test(content)) {
        return content.replace(dateRegex, `$1${today}`);
    }
    return content;
}

// 2. 统计代码
function countCodes(dir) {
    let files = 0;
    let lines = 0;

    function traverse(currentPath) {
        const entries = fs.readdirSync(currentPath, { withFileTypes: true });
        for (const entry of entries) {
            const fullPath = path.join(currentPath, entry.name);
            if (entry.isDirectory()) {
                traverse(fullPath);
            } else if (entry.isFile() && entry.name.endsWith('.cs')) {
                files++;
                const content = fs.readFileSync(fullPath, 'utf-8');
                lines += content.split('\n').length;
            }
        }
    }

    if (fs.existsSync(dir)) {
        traverse(dir);
    }
    return { files, lines };
}

// 3. 更新统计章节
function updateStats(content, stats) {
    const statsHeader = '## 7. 代码统计';
    const statsContent = `
- **统计时间**: ${new Date().toLocaleString()}
- **源文件目录**: \`MapExt/\`
- **C# 文件数**: ${stats.files}
- **总行数**: ${stats.lines}
`;

    if (content.includes(statsHeader)) {
        // 替换现有章节
        const parts = content.split(statsHeader);
        return parts[0] + statsHeader + statsContent;
    } else {
        // 追加新章节
        return content + '\n\n' + statsHeader + statsContent;
    }
}

// 主流程
try {
    console.log('正在读取文档...');
    let content = fs.readFileSync(docPath, 'utf-8');

    console.log('正在更新日期...');
    content = updateDate(content);

    console.log('正在统计代码...');
    const stats = countCodes(srcPath);
    console.log(`扫描到 ${stats.files} 个文件, ${stats.lines} 行代码。`);

    console.log('正在更新统计信息...');
    content = updateStats(content, stats);

    fs.writeFileSync(docPath, content, 'utf-8');
    console.log('文档更新成功！');
} catch (err) {
    console.error('更新失败:', err);
    process.exit(1);
}
