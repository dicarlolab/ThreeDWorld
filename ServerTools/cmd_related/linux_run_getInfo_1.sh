#for k in $(seq 10000 1000 30000)
#for k in $(seq 10000 1000 20000)
#for k in $(seq 11000 1000 20000)
for k in 10000
do
    echo ${k}
    python cmdRun_getInfo_batch.py -n 10 -s ${k}
done
