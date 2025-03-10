import React, { useState, useEffect } from 'react';
import { 
  Container, Grid, Card, CardContent, CardHeader, 
  Typography, Box, Chip, LinearProgress, Button,
  Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, IconButton, Tooltip
} from '@mui/material';
import { 
  BugReport as BugIcon,
  Security as SecurityIcon,
  Speed as PerformanceIcon,
  Code as StyleIcon,
  Warning as WarningIcon,
  GitHub as GitHubIcon,
  Refresh as RefreshIcon,
  ArrowUpward as ArrowUpIcon,
  ArrowDownward as ArrowDownIcon
} from '@mui/icons-material';
import { 
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, 
  Legend, ResponsiveContainer, PieChart, Pie, Cell
} from 'recharts';
import { formatDistanceToNow } from 'date-fns';
import api from '../services/api';
