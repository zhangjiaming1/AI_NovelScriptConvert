import yaml
from datetime import datetime

def novel_to_script(raw_text):
    data = {
        "剧本信息":{
            "剧本名称":"小城旧事",
            "原著名称":"小城旧事",
            "总章节":3,
            "生成日期":datetime.now().strftime("%Y-%m-%d")
        },
        "场景列表":[
            {
                "场景序号":1,
                "地点":"老街杂货铺",
                "场景分类":"内景",
                "时段":"白天",
                "环境介绍":"阳光透过窗户照进老旧小店",
                "出场人物":[{"姓名":"老李","人物介绍":"50岁杂货店店主"}],
                "台词动作":[
                    {"条目编号":1,"动作描述":"擦拭玻璃瓶子","发言人":"老李","台词":"又来吃糖？"}
                ]
            }
        ]
    }
    return data

if __name__ == "__main__":
    with open("../test_data/原文.txt","r",encoding="utf-8") as f:
        novel_content = f.read()
    res = novel_to_script(novel_content)
    with open("../test_data/生成剧本.yaml","w",encoding="utf-8") as f:
        yaml.dump(res,f,allow_unicode=True,sort_keys=False)
    print("转换完成")