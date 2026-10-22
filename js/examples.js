

// 开局选择时期
baye.hooks.loadPeriod = function() {
    baye.centerChoose(100, 80, ["简单", "困难"], 0, function(idx) {
        console.log('chosen: ' + idx);
        if (idx == baye.None) {
            return 0; // 0 返回主界面， 1 继续选择君主
        }
        baye.loadPeriod(1); // 加载时期1数据
        // 可以在这里进一步做其它初始化配置
        return 1; // 继续选择君主
    });
}

// 菜单空闲时每隔大约半秒多调用一次onMenuIdle
baye.hooks.onMenuIdle = function(ctx) {
    baye.drawText(0, 0, "selected:" + ctx.index);
    var x = 8*ctx.index;
    var y = 20;
    var w = 8, h = 8;
    baye.revertRect(x, y, x+w-1, y+h-1);
}

// 战场系统菜单
baye.hooks.fightOpenMainMenu = function() {
    baye.centerChoose(60, 30, ["回合结束", "全军撤退"], 0, function(idx) {
        // 返回值被当做原系统菜单的选择项
        return idx;
    });
}

// 战场移动将领后动作选择菜单
baye.hooks.fightChooseAction = function(ctx) {
    console.log("person = " + ctx.index);
    baye.centerChoose(30, 60, ["攻击", "计谋", "查看", "待机"], 0, function(idx) {
        // 返回值被当做原始菜单的选择项
        return idx;
    });
}

// 战场技能选择菜单
baye.hooks.fightChooseSkill = function(ctx) {
    console.log("person = " + ctx.index);
    var skills = [2, 3, 4];
    var skillNames = skills.map(x => baye.getSkillName(x)); // 注意有些浏览器没有map函数

    baye.centerChoose(30, 60, skillNames, 0, function(idx) {
        if (idx == baye.None) {
            // 取消选择
            return baye.None;
        } else {
            // 返回技能ID
            return skills[idx];
        }
    });
}

// 设置战场全军撤退是否需要二次确认
baye.data.g_engineConfig.confirmOnEscape = 1;

// 设置是否开启像素模糊化, 高DPI时，有些情况可能开启blur会好看点
baye.blurScreen(true);







var currentFont = 0;

function setFont(font) {
    if (baye.setFont(font) == baye.OK) {
        currentFont = font;
    }
}

baye.hooks.willCloseMenu = function(ctx) {
    baye.hooks.willChangeMenuSelection = null;
};

baye.hooks.showMainHelp = function() {
    baye.hooks.willChangeMenuSelection = function(ctx) {
        var oldFont = currentFont; // 备份当前字体
        setFont(ctx.index);
        baye.clearRect(0, 0, 60, 30)
        baye.drawText(0, 0, "字体预览")
        setFont(oldFont); // 预览完恢复当前字体
    };
    baye.centerChoose(28, 50, ["默认", "仿宋", "黑体", "楷体", "宋体"], currentFont, function(idx) {
        if (idx != baye.None) {
            setFont(idx);
        }
    });
}


