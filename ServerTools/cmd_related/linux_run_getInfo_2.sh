#for k in $(seq 10000 1000 30000)
for k in $(seq 21000 1000 31000)
do
    echo ${k}
    python cmdRun_getInfo_batch.py -n 11 -s ${k}
done
