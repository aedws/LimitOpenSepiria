# 블루비틀 퓨리 상태 아이콘(18x18): 본판 상태 아이콘과 같은 짜임(2픽셀 검은 테, 색 바탕, 흰 무늬).
from PIL import Image
import sys
K=(0,0,0,255); B=(62,112,204,255); W=(255,255,255,255); C=(151,199,254,255)
im=Image.new('RGBA',(18,18),(0,0,0,0)); px=im.load()
for y in range(18):
    for x in range(18):
        if (x in (0,17) and y in (0,17)): continue
        edge=min(x,y,17-x,17-y)
        px[x,y]=K if edge<2 or (x in (2,15) and y in (2,15)) else B
for i in range(10): px[4+i,13-i]=W                 # 창 자루
for p in ((11,4),(12,4),(13,5),(13,6),(12,5),(11,5),(12,6),(10,5),(12,7)): px[p]=W   # 창끝
for p in ((4,9),(5,8),(6,7),(8,13),(9,12),(10,11)): px[p]=C    # 돌진선
im.save(sys.argv[1]); im.resize((180,180),Image.NEAREST).save(sys.argv[2])
